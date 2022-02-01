// Copyright (c) 2016 Perfare - https://github.com/Perfare/Il2CppDumper/
// Copyright (c) 2016 Alican Çubukçuoğlu - https://github.com/AlicanC/AlicanC-s-Modern-Warfare-2-Tool/
// Copyright (c) 2017-2021 Katy Coe - http://www.djkaty.com - https://github.com/djkaty/Bin2Object/

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
        // Method cache for primitive mappings
        private Dictionary<string, MethodInfo> readMethodCache { get; }

        // Generic method cache to dramatically speed up repeated calls to ReadObject<T> with the same T
        private Dictionary<string, MethodInfo> readObjectGenericCache = new Dictionary<string, MethodInfo>();

        // VersionAttribute cache to dramatically speed up repeated calls to ReadObject<T> with the same T
        private Dictionary<Type, Dictionary<FieldInfo, List<(double Min, double Max)>>> readObjectVersionCache = new Dictionary<Type, Dictionary<FieldInfo, List<(double, double)>>>();

        // Thread synchronization objects (for thread safety)
        private object readLock = new object();

        // Initialization
        public BinaryObjectReader(Stream stream, Endianness endianness = Endianness.Little, bool leaveOpen = false) : base(stream, Encoding.Default, leaveOpen) {
            Endianness = endianness;

            readMethodCache = typeof(BinaryObjectReader).GetMethods().Where(m => m.Name.StartsWith("Read") && !m.GetParameters().Any()).GroupBy(m => m.ReturnType).ToDictionary(kv => kv.Key.Name, kv => kv.First());
        }

        // Position in the stream
        public long Position {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        // Allows you to specify primitive types which should be read as different types in the stream
        // Key: type in object; Value: type in stream
        public Dictionary<string, Type> PrimitiveMappings { get; } = new Dictionary<string, Type>();

        // Allows you to specify object types which should be read as different types in the stream
        // The fields of the read object will be copied to the fields of the target object with matching names
        // Key: object type to return; Value: object type to read from stream
        public Dictionary<Type, Type> ObjectMappings { get; } = new Dictionary<Type, Type>();

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

        private object ReadPrimitive(Type t) {
            // Checked for mapped primitive types
            if (PrimitiveMappings.TryGetValue(t.Name, out Type mapping)) {
                var mappedReader = readMethodCache[mapping.Name];
                var result = mappedReader.Invoke(this, null);
                return Convert.ChangeType(result, t);
            }

            // Unmapped primitive (eliminating obj causes Visual Studio 16.3.5 to crash)
            object obj = t.Name switch {
                "Int64" => ReadInt64(),
                "UInt64" => ReadUInt64(),
                "Int32" => ReadInt32(),
                "UInt32" => ReadUInt32(),
                "Int16" => ReadInt16(),
                "UInt16" => ReadUInt16(),
                "Byte" => ReadByte(),
                "Boolean" => ReadBoolean(),
                _ => throw new ArgumentException("Unsupported primitive type specified: " + t.FullName)
            };
            return obj;
        }

        // Copy matching named fields from one object to another
        // Fields in the source not in the target and vice versa are ignored
        private T MapObject<T>(object from) where T : new() {
            var t = new T();

            var fromType = from.GetType();
            var toTypeFields = typeof(T).GetFields();

            // Iterate source fields            
            foreach (var f in fromType.GetFields()) {
                // Find target field with matching name - ignore the rest
                if (toTypeFields.FirstOrDefault(tf => tf.Name == f.Name) is FieldInfo targetField) {
                    targetField.SetValue(t, Convert.ChangeType(f.GetValue(from), targetField.FieldType));
                }
            }

            return t;
        }

        public T ReadObject<T>() where T : new() {
            var type = typeof(T);

            // Object is actually a primitive
            if (type.IsPrimitive) {
                return (T) ReadPrimitive(type);
            }

            // Check for object mapping
            if (ObjectMappings.TryGetValue(type, out var streamType)) {
                if (!readObjectGenericCache.TryGetValue(streamType.FullName, out MethodInfo mi2)) {
                    var us = GetType().GetMethod("ReadObject", Type.EmptyTypes);
                    mi2 = us.MakeGenericMethod(streamType);
                    readObjectGenericCache.Add(streamType.FullName, mi2);
                }

                var obj = mi2.Invoke(this, null);

                return MapObject<T>(obj);
            }

            var t = new T();

            // First time caching
            if (!readObjectVersionCache.ContainsKey(type)) {
                var fields = new Dictionary<FieldInfo, List<(double, double)>>();
                foreach (var i in type.GetFields())
                    if (i.GetCustomAttribute<SkipWhenReadingAttribute>(false) is SkipWhenReadingAttribute)
                        fields.Add(i, new List<(double, double)> {(-2, -2)});
                    else
                        fields.Add(i, i.GetCustomAttributes<VersionAttribute>(false).Select(v => (v.Min, v.Max)).ToList());

                readObjectVersionCache.Add(type, fields);
            }

            foreach (var (i, versions) in readObjectVersionCache[type]) {
                // Skip fields with SkipWhenReading set
                if (versions.FirstOrDefault() == (-2, -2))
                    continue;

                // Only process fields for our selected object versioning (always process if none supplied)
                if (versions.Any() && !versions.Any(v => (v.Min <= Version || v.Min == -1) && (v.Max >= Version || v.Max == -1)))
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
                    i.SetValue(t, mi2.Invoke(this, new object[] {lengthPrimitive}));
                }

                // Primitive type
                // This is unnecessary but saves on many generic Invoke calls which are really slow
                else if (i.FieldType.IsPrimitive) {
                    i.SetValue(t, ReadPrimitive(i.FieldType));
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

            if (!typeof(T).IsPrimitive) {
                for (int i = 0; i < count; i++)
                    t[i] = ReadObject<T>();
            }
            else {
                var type = typeof(T);

                // Checked for mapped primitive types
                if (PrimitiveMappings.TryGetValue(type.Name, out Type mapping)) {
                    var mappedReader = readMethodCache[mapping.Name];

                    for (var i = 0; i < count; i++)
                        t[i] = (T) Convert.ChangeType(mappedReader.Invoke(this, null), type);
                }
                else {
                    // Unmapped primitive (eliminating obj causes Visual Studio 16.3.5 to crash)
                    for (var i = 0; i < count; i++)
                        t[i] = (T) (type.Name switch {
                            "Int64" => (object) ReadInt64(),
                            "UInt64" => ReadUInt64(),
                            "Int32" => ReadInt32(),
                            "UInt32" => ReadUInt32(),
                            "Int16" => ReadInt16(),
                            "UInt16" => ReadUInt16(),
                            "Byte" => ReadByte(),
                            "Boolean" => ReadBoolean() ? 1ul : 0ul,
                            _ => throw new ArgumentException("Unsupported primitive type specified: " + type.FullName)
                        });
                }
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

        public uint ReadUnityCompressedUInt(long addr, out int bytesRead) {
            lock (readLock) {
                if(Position >= 0)
                    Position = addr;

                //Read first byte
                byte b = ReadByte();
                bytesRead = 1;
                if (b < 128)
                    return b;
                
                switch (b) {
                    case 240:
                        //Upper four bits set, lower four not => read full uint
                        bytesRead = 5;
                        return ReadUInt32();
                    case byte.MaxValue:
                        //All 8 bits set => max uint
                        return uint.MaxValue;
                    case 254:
                        //Upper 7 bits set => max uint - 1
                        return uint.MaxValue - 1;
                    case { } when (b & 192) == 192:
                        //Upper two bits set => read three more bytes
                        bytesRead = 4;
                        return (b & ~192U) << 24 | (uint) (ReadByte() << 16) | (uint) (ReadByte() << 8) | ReadByte();
                    case {} when (b & 128) == 128:
                        //Upper bit set => read one more byte
                        bytesRead = 2;
                        return (b & ~128U) << 8 | ReadByte();
                    default:
                        //This case shouldn't be possible - if the top bit isn't set then we returned further up,
                        //And any other scenario where the top bit is set is covered by the above
                        throw new Exception($"Invalid compressed int first byte {b}");
                }
            }
        }

        public int ReadUnityCompressedInt(long addr, out int bytesRead) {
            //Basically just read as a uint then translate one special value and handle negatives
            uint unsigned = ReadUnityCompressedUInt(addr, out bytesRead);

            if (unsigned == uint.MaxValue)
                //Max uint just becomes max int
                return int.MaxValue;
            
            //*Lowest* bit is sign bit, not highest - gotta admit this is pretty clever
            bool isNegative = (unsigned & 1) == 1;
            
            //Discard sign bit
            unsigned >>= 1;
            
            if (isNegative)
                //Negative values are offset by 1 so that minint can be stored
                return -(int) (unsigned + 1);

            //Positive values - just cast as int
            return (int) unsigned;
        }
    }
}