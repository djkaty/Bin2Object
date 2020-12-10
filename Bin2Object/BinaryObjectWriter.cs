// Copyright (c) 2020 Katy Coe - http://www.djkaty.com - https://github.com/djkaty/Bin2Object/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NoisyCowStudios.Bin2Object
{
    public class BinaryObjectWriter : BinaryWriter
    {
        // Generic method cache to dramatically speed up repeated calls to WriteObject<T> with the same T
        private Dictionary<string, MethodInfo> writeObjectGenericCache = new Dictionary<string, MethodInfo>();

        // VersionAttribute cache to dramatically speed up repeated calls to ReadObject<T> with the same T
        private Dictionary<Type, Dictionary<FieldInfo, (double Min, double Max)>> writeObjectVersionCache = new Dictionary<Type, Dictionary<FieldInfo, (double, double)>>();

        // Thread synchronization objects (for thread safety)
        private object writeLock = new object();

        // Initialization
        public BinaryObjectWriter(Stream stream, Endianness endianness = Endianness.Little) : base(stream) {
            Endianness = endianness;
        }

        // Position in the stream
        public long Position {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        // Allows you to specify types which should be written as different types to the stream
        // Key: type in object; Value: type in stream
        public Dictionary<Type, Type> PrimitiveMappings { get; } = new Dictionary<Type, Type>();

        public Endianness Endianness { get; set; }

        public double Version { get; set; } = 1;

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public void WriteEndianBytes(byte[] bytes) {
            Write(Endianness == Endianness.Little ? bytes : bytes.Reverse().ToArray());
        }

        public override void Write(long int64) => WriteEndianBytes(BitConverter.GetBytes(int64));

        public override void Write(ulong uint64) => WriteEndianBytes(BitConverter.GetBytes(uint64));

        public override void Write(int int32) => WriteEndianBytes(BitConverter.GetBytes(int32));

        public override void Write(uint uint32) => WriteEndianBytes(BitConverter.GetBytes(uint32));

        public override void Write(short int16) => WriteEndianBytes(BitConverter.GetBytes(int16));

        public override void Write(ushort uint16) => WriteEndianBytes(BitConverter.GetBytes(uint16));

        public void Write(long addr, byte[] bytes) {
            lock (writeLock) {
                Position = addr;
                WriteEndianBytes(bytes);
            }
        }

        public void Write(long addr, long int64) {
            lock (writeLock) {
                Position = addr;
                Write(int64);
            }
        }

        public void Write(long addr, ulong uint64) {
            lock (writeLock) {
                Position = addr;
                Write(uint64);
            }
        }

        public void Write(long addr, int int32) {
            lock (writeLock) {
                Position = addr;
                Write(int32);
            }
        }

        public void Write(long addr, uint uint32) {
            lock (writeLock) {
                Position = addr;
                Write(uint32);
            }
        }

        public void Write(long addr, short int16) {
            lock (writeLock) {
                Position = addr;
                Write(int16);
            }
        }

        public void Write(long addr, ushort uint16) {
            lock (writeLock) {
                Position = addr;
                Write(uint16);
            }
        }

        public void Write(long addr, byte value) {
            lock (writeLock) {
                Position = addr;
                Write(value);
            }
        }

        public void Write(long addr, bool value) {
            lock (writeLock) {
                Position = addr;
                Write(value);
            }
        }

        public void WriteObject<T>(long addr, T obj) {
            lock (writeLock) {
                Position = addr;
                WriteObject(obj);
            }
        }

        public void WriteObject<T>(T obj) {
            var type = typeof(T);
            var ti = type.GetTypeInfo();

            if (ti.IsPrimitive) {
                // Checked for mapped primitive types
                if ((from m in PrimitiveMappings where m.Key.GetTypeInfo().Name == type.Name select m.Value).FirstOrDefault() is Type mapping) {
                    var mappedWriter = (from m in GetType().GetMethods() where m.Name == "Write" && m.GetParameters()[0].ParameterType == mapping && m.ReturnType == typeof(void) select m).FirstOrDefault();
                    mappedWriter?.Invoke(this, new object[] { obj });
                    return;
                }

                // Unmapped primitive
                switch (obj) {
                    case long v:
                        Write(v);
                        break;
                    case ulong v:
                        Write(v);
                        break;
                    case int v:
                        Write(v);
                        break;
                    case uint v:
                        Write(v);
                        break;
                    case short v:
                        Write(v);
                        break;
                    case ushort v:
                        Write(v);
                        break;
                    case byte v:
                        Write(v);
                        break;
                    case bool v:
                        Write(v);
                        break;
                    default:
                        throw new ArgumentException("Unsupported primitive type specified: " + type.FullName);
                }
                return;
            }

            // First time caching
            if (!writeObjectVersionCache.TryGetValue(type, out var cachedFields)) {
                var fields = new Dictionary<FieldInfo, (double, double)>();
                foreach (var i in type.GetFields())
                    if (i.GetCustomAttribute<VersionAttribute>(false) is VersionAttribute versionAttr)
                        fields.Add(i, (versionAttr.Min, versionAttr.Max));
                    else
                        fields.Add(i, (-1, -1));
                writeObjectVersionCache.Add(type, fields);
            }

            foreach (var (i, version) in writeObjectVersionCache[type]) {
                // Only process fields for our selected object versioning
                if (version.Min != -1 && version.Min > Version)
                    continue;
                if (version.Max != -1 && version.Max < Version)
                    continue;

                // String
                if (i.FieldType == typeof(string)) {
                    var attr = i.GetCustomAttribute<StringAttribute>(false);

                    // No String attribute? Use a null-terminated string by default
                    if (attr == null || attr.IsNullTerminated)
                        WriteNullTerminatedString((string) i.GetValue(obj));
                    else {
                        if (attr.FixedSize <= 0)
                            throw new ArgumentException("String attribute for array field " + i.Name + " configuration invalid");
                        WriteFixedLengthString((string) i.GetValue(obj), attr.FixedSize);
                    }
                }

                // Array
                else if (i.FieldType.IsArray) {
                    var attr = i.GetCustomAttribute<ArrayLengthAttribute>(false) ??
                        throw new InvalidOperationException("Array field " + i.Name + " must have ArrayLength attribute");

                    int lengthPrimitive;

                    if (attr.FieldName != null) {
                        var field = type.GetField(attr.FieldName) ??
                            throw new ArgumentException("Array field " + i.Name + " has invalid FieldName in ArrayLength attribute");
                        lengthPrimitive = Convert.ToInt32(field.GetValue(obj));
                    } else if (attr.FixedSize > 0) {
                        lengthPrimitive = attr.FixedSize;
                    } else {
                        throw new ArgumentException("ArrayLength attribute for array field " + i.Name + " configuration invalid");
                    }

                    var arr = i.GetValue(obj);
                    var us = GetType().GetMethods().Where(m => m.Name == "WriteArray" && m.GetParameters().Length == 1 && m.IsGenericMethodDefinition).Single();
                    var mi2 = us.MakeGenericMethod(i.FieldType.GetElementType());
                    mi2.Invoke(this, new object[] { arr });
                }

                // Primitive type
                // This is unnecessary but saves on many generic Invoke calls which are really slow
                else if (i.FieldType.IsPrimitive) {
                    // Checked for mapped primitive types
                    if ((from m in PrimitiveMappings where m.Key.GetTypeInfo().Name == i.FieldType.Name select m.Value).FirstOrDefault() is Type mapping) {
                        var mappedWriter = (from m in GetType().GetMethods() where m.Name == "Write" && m.GetParameters()[0].ParameterType == mapping && m.ReturnType == typeof(void) select m).FirstOrDefault();
                        mappedWriter?.Invoke(this, new object[] { Convert.ChangeType(i.GetValue(obj), mapping) });
                    } else {
                        // Unmapped primitive type
                        switch (i.GetValue(obj)) {
                            case long v:
                                Write(v);
                                break;
                            case ulong v:
                                Write(v);
                                break;
                            case int v:
                                Write(v);
                                break;
                            case uint v:
                                Write(v);
                                break;
                            case short v:
                                Write(v);
                                break;
                            case ushort v:
                                Write(v);
                                break;
                            case byte v:
                                Write(v);
                                break;
                            case bool v:
                                Write(v);
                                break;
                            default:
                                throw new ArgumentException("Unsupported primitive type specified: " + type.FullName);
                        }
                    }
                }

                // Object
                else {
                    if (!writeObjectGenericCache.TryGetValue(i.FieldType.FullName, out MethodInfo mi2)) {
                        var us = GetType().GetMethods().Where(m => m.Name == "WriteObject" && m.IsGenericMethodDefinition).Single();
                        mi2 = us.MakeGenericMethod(i.FieldType);
                        writeObjectGenericCache.Add(i.FieldType.FullName, mi2);
                    }
                    mi2.Invoke(this, new[] { i.GetValue(obj) });
                }
            }
        }

        public void WriteArray<T>(long addr, T[] array) {
            lock (writeLock) {
                Position = addr;
                WriteArray(array);
            }
        }

        public void WriteArray<T>(T[] array) {
            for (int i = 0; i < array.Length; i++) {
                WriteObject(array[i]);
            }
        }

        public void WriteNullTerminatedString(long addr, string str, Encoding encoding = null) {
            lock (writeLock) {
                Position = addr;
                WriteNullTerminatedString(str, encoding);
            }
        }

        public void WriteNullTerminatedString(string str, Encoding encoding = null) {
            WriteFixedLengthString(str, str.Length + 1, encoding);
        }

        // The difference between this and BinaryWriter.Write(string) is that the latter adds a length prefix before the string
        public void WriteFixedLengthString(long addr, string str, int size = -1, Encoding encoding = null) {
            lock (writeLock) {
                Position = addr;
                WriteFixedLengthString(str, size, encoding);
            }
        }

        public void WriteFixedLengthString(string str, int size = -1, Encoding encoding = null) {
            var bytes = encoding?.GetBytes(str) ?? Encoding.GetBytes(str);
            Write(bytes);

            if (size != -1)
                for (var padding = str.Length; padding < size; padding++)
                    Write((byte) 0);
        }
    }
}
