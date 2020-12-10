// Copyright (c) 2016 Perfare - https://github.com/Perfare/Il2CppDumper/
// Copyright (c) 2016 Alican Çubukçuoğlu - https://github.com/AlicanC/AlicanC-s-Modern-Warfare-2-Tool/
// Copyright (c) 2017-2020 Katy Coe - http://www.djkaty.com - https://github.com/djkaty/Bin2Object/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NoisyCowStudios.Bin2Object
{
    public class BinaryObjectReader : BinaryReader
    {
        // Generic method cache to dramatically speed up repeated calls to ReadObject<T> with the same T
        private Dictionary<string, MethodInfo> readObjectGenericCache = new Dictionary<string, MethodInfo>();

        // VersionAttribute cache to dramatically speed up repeated calls to ReadObject<T> with the same T
        private Dictionary<Type, Dictionary<FieldInfo, (double Min, double Max)>> readObjectVersionCache = new Dictionary<Type, Dictionary<FieldInfo, (double, double)>>();

        // Thread synchronization objects (for thread safety)
        private object readLock = new object();

        // Initialization
        public BinaryObjectReader(Stream stream, Endianness endianness = Endianness.Little) : base(stream) {
            Endianness = endianness;
        }

        // Position in the stream
        public long Position {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        // Allows you to specify types which should be read as different types in the stream
        // Key: type in object; Value: type in stream
        public Dictionary<Type, Type> PrimitiveMappings { get; } = new Dictionary<Type, Type>();

        public Endianness Endianness { get; set; }

        public double Version { get; set; } = 1;

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public override byte[] ReadBytes(int count) {
            var bytes = base.ReadBytes(count);
            return (Endianness == Endianness.Little ? bytes : bytes.Reverse().ToArray());
        }

        public override long ReadInt64() => BitConverter.ToInt64(ReadBytes(8), 0);
        
        public override ulong ReadUInt64() => BitConverter.ToUInt64(ReadBytes(8), 0);

        public override int ReadInt32() => BitConverter.ToInt32(ReadBytes(4), 0);

        public override uint ReadUInt32() => BitConverter.ToUInt32(ReadBytes(4), 0);

        public override short ReadInt16() => BitConverter.ToInt16(ReadBytes(2), 0);

        public override ushort ReadUInt16() => BitConverter.ToUInt16(ReadBytes(2), 0);

        public byte[] ReadBytes(long addr, int count) {
            lock (readLock) {
                Position = addr;
                return ReadBytes(count);
            }
        }

        public long ReadInt64(long addr) {
            lock (readLock) {
                Position = addr;
                return ReadInt64();
            }
        }

        public ulong ReadUInt64(long addr) {
            lock (readLock) {
                Position = addr;
                return ReadUInt64();
            }
        }

        public int ReadInt32(long addr) {
            lock (readLock) {
                Position = addr;
                return ReadInt32();
            }
        }

        public uint ReadUInt32(long addr) {
            lock (readLock) {
                Position = addr;
                return ReadUInt32();
            }
        }

        public short ReadInt16(long addr) {
            lock (readLock) {
                Position = addr;
                return ReadInt16();
            }
        }

        public ushort ReadUInt16(long addr) {
            lock (readLock) {
                Position = addr;
                return ReadUInt16();
            }
        }

        public byte ReadByte(long addr) {
            lock (readLock) {
                Position = addr;
                return ReadByte();
            }
        }

        public bool ReadBoolean(long addr) {
            lock (readLock) {
                Position = addr;
                return ReadBoolean();
            }
        }

        public T ReadObject<T>(long addr) where T : new() {
            lock (readLock) {
                Position = addr;
                return ReadObject<T>();
            }
        }

        public T ReadObject<T>() where T : new() {
            var type = typeof(T);
            var ti = type.GetTypeInfo();

            if (ti.IsPrimitive) {
                // Checked for mapped primitive types
                if ((from m in PrimitiveMappings where m.Key.GetTypeInfo().Name == type.Name select m.Value).FirstOrDefault() is Type mapping) {
                    var mappedReader = (from m in GetType().GetMethods() where m.Name.StartsWith("Read") && m.ReturnType == mapping && !m.GetParameters().Any() select m).FirstOrDefault();
                    return (T) Convert.ChangeType(mappedReader?.Invoke(this, null), typeof(T));
                }

                // Unmapped primitive (eliminating obj causes Visual Studio 16.3.5 to crash)
                object obj = type.Name switch {
                    "Int64" => ReadInt64(),
                    "UInt64" => ReadUInt64(),
                    "Int32" => ReadInt32(),
                    "UInt32" => ReadUInt32(),
                    "Int16" => ReadInt16(),
                    "UInt16" => ReadUInt16(),
                    "Byte" => ReadByte(),
                    "Boolean" => ReadBoolean(),
                    _ => throw new ArgumentException("Unsupported primitive type specified: " + type.FullName)
                };
				return (T) obj;
			}

            var t = new T();

            // First time caching
            if (!readObjectVersionCache.TryGetValue(type, out var cachedFields)) {
                var fields = new Dictionary<FieldInfo, (double, double)>();
                foreach (var i in type.GetFields())
                    if (i.GetCustomAttribute<VersionAttribute>(false) is VersionAttribute versionAttr)
                        fields.Add(i, (versionAttr.Min, versionAttr.Max));
                    else
                        fields.Add(i, (-1, -1));
                readObjectVersionCache.Add(type, fields);
            }

            foreach (var (i, version) in readObjectVersionCache[type]) {
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
                        i.SetValue(t, ReadNullTerminatedString());
                    else {
                        if (attr.FixedSize <= 0)
                            throw new ArgumentException("String attribute for array field " + i.Name + " configuration invalid");
                        i.SetValue(t, ReadFixedLengthString(attr.FixedSize));
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
                        lengthPrimitive = Convert.ToInt32(field.GetValue(t));
                    }
                    else if (attr.FixedSize > 0) {
                        lengthPrimitive = attr.FixedSize;
                    }
                    else {
                        throw new ArgumentException("ArrayLength attribute for array field " + i.Name + " configuration invalid");
                    }

                    var us = GetType().GetMethod("ReadArray", new[] {typeof(int)});
                    var mi2 = us.MakeGenericMethod(i.FieldType.GetElementType());
                    i.SetValue(t, mi2.Invoke(this, new object[] { lengthPrimitive }));
                }

                // Primitive type
                // This is unnecessary but saves on many generic Invoke calls which are really slow
                else if (i.FieldType.IsPrimitive) {
                    // Checked for mapped primitive types
                    var mapping = (from m in PrimitiveMappings where m.Key.GetTypeInfo().Name == i.FieldType.Name select m).FirstOrDefault();
                    if (!mapping.Equals(default(KeyValuePair<Type,Type>))) {
                        var mappedReader = (from m in GetType().GetMethods() where m.Name.StartsWith("Read") && m.ReturnType == mapping.Value && !m.GetParameters().Any() select m).FirstOrDefault();
                        i.SetValue(t, Convert.ChangeType(mappedReader?.Invoke(this, null), mapping.Key));
                    }
                    else {
                        // Unmapped primitive type
                        i.SetValue(t, i.FieldType.Name switch {
                            "Int64" => ReadInt64(),
                            "UInt64" => ReadUInt64(),
                            "Int32" => ReadInt32(),
                            "UInt32" => ReadUInt32(),
                            "Int16" => ReadInt16(),
                            "UInt16" => ReadUInt16(),
                            "Byte" => ReadByte(),
                            "Boolean" => ReadBoolean(),
                            _ => throw new ArgumentException("Unsupported primitive type specified: " + i.FieldType.FullName)
                        });
                    }
                }

                // Object
                else {
                    if (!readObjectGenericCache.TryGetValue(i.FieldType.FullName, out MethodInfo mi2)) {
                        var us = GetType().GetMethod("ReadObject", Type.EmptyTypes);
                        mi2 = us.MakeGenericMethod(i.FieldType);
                        readObjectGenericCache.Add(i.FieldType.FullName, mi2);
                    }
                    i.SetValue(t, mi2.Invoke(this, null));
                }
            }
            return t;
        }

        public T[] ReadArray<T>(long addr, int count) where T : new() {
            lock (readLock) {
                Position = addr;
                return ReadArray<T>(count);
            }
        }

        public T[] ReadArray<T>(int count) where T : new() {
            T[] t = new T[count];
            for (int i = 0; i < count; i++) {
                t[i] = ReadObject<T>();
            }
            return t;
        }

        public string ReadNullTerminatedString(long addr, Encoding encoding = null) {
            lock (readLock) {
                Position = addr;
                return ReadNullTerminatedString(encoding);
            }
        }

        public string ReadNullTerminatedString(Encoding encoding = null) {
            List<byte> bytes = new List<byte>();
            byte b;
            while ((b = ReadByte()) != 0)
                bytes.Add(b);
            return encoding?.GetString(bytes.ToArray()) ?? Encoding.GetString(bytes.ToArray());
        }

        public string ReadFixedLengthString(long addr, int length, Encoding encoding = null) {
            lock (readLock) {
                Position = addr;
                return ReadFixedLengthString(length, encoding);
            }
        }

        public string ReadFixedLengthString(int length, Encoding encoding = null) {
            byte[] b = ReadArray<byte>(length);
            List<byte> bytes = new List<byte>();
            foreach (var c in b)
                if (c == 0)
                    break;
                else
                    bytes.Add(c);
            return encoding?.GetString(bytes.ToArray()) ?? Encoding.GetString(bytes.ToArray());
        }
    }
}
