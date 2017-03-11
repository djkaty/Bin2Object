// Copyright (c) 2016 Perfare - https://github.com/Perfare/Il2CppDumper/
// Copyright (c) 2016 Alican Çubukçuoğlu - https://github.com/AlicanC/AlicanC-s-Modern-Warfare-2-Tool/
// Copyright (c) 2017 Katy Coe - http://www.djkaty.com - https://github.com/djkaty/Bin2Object/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NoisyCowStudios.Bin2Object
{
    public enum Endianness
    {
        Little,
        Big
    }

    public class BinaryObjectReader : BinaryReader
    {
        public BinaryObjectReader(Stream stream, Endianness endianness = Endianness.Little) : base(stream) {
            Endianness = endianness;
        }

        public long Position {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public Endianness Endianness { get; set; }

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public override byte[] ReadBytes(int count) {
            var bytes = base.ReadBytes(count);
            return (Endianness == Endianness.Little ? bytes : bytes.Reverse().ToArray());
        }

        public override long ReadInt64() {
            return BitConverter.ToInt64(ReadBytes(8), 0);
        }

        public override ulong ReadUInt64() {
            return BitConverter.ToUInt64(ReadBytes(8), 0);
        }

        public override int ReadInt32() {
            return BitConverter.ToInt32(ReadBytes(4), 0);
        }

        public override uint ReadUInt32() {
            return BitConverter.ToUInt32(ReadBytes(4), 0);
        }

        public override short ReadInt16() {
            return BitConverter.ToInt16(ReadBytes(2), 0);
        }

        public override ushort ReadUInt16() {
            return BitConverter.ToUInt16(ReadBytes(2), 0);
        }

        public T ReadObject<T>(long addr) where T : new() {
            Position = addr;
            return ReadObject<T>();
        }

        public T ReadObject<T>() where T : new() {
            var type = typeof(T);
            var ti = type.GetTypeInfo();

            if (ti.IsPrimitive) {
                object obj;
                switch (type.Name) {
                    case "Int64":
                        obj = ReadInt64();
                        break;
                    case "UInt64":
                        obj = ReadUInt64();
                        break;
                    case "Int32":
                        obj = ReadInt32();
                        break;
                    case "UInt32":
                        obj = ReadUInt32();
                        break;
                    case "Int16":
                        obj = ReadInt16();
                        break;
                    case "UInt16":
                        obj = ReadUInt16();
                        break;
                    case "Byte":
                        obj = ReadByte();
                        break;
                    case "Boolean":
                        obj = ReadBoolean();
                        break;
                    default:
                        throw new ArgumentException("Unsupported primitive type specified: " + type.FullName);
                }
                return (T)obj;
            }

            var t = new T();
            foreach (var i in t.GetType().GetFields()) {
                if (i.FieldType.FullName == "System.String") {
                    i.SetValue(t, ReadNullTerminatedString());
                }
                else if (i.FieldType.IsArray) {
                    var attr = i.GetCustomAttribute<ArrayLengthAttribute>(false);
                    if (attr == null)
                        throw new InvalidOperationException("Array field " + i.Name + " must have ArrayLength attribute");

                    var field = type.GetField(attr.FieldName);
                    if (field == null)
                        throw new ArgumentException("Array field " + i.Name + " has invalid FieldName in ArrayLength attribute");

                    var lengthPrimitive = field.GetValue(t);
                    var us = GetType().GetMethod("ReadArray", new[] {typeof(int)});
                    var mi2 = us.MakeGenericMethod(i.FieldType.GetElementType());
                    i.SetValue(t, mi2.Invoke(this, new[] { lengthPrimitive }));
                }
                else {
                    var us = GetType().GetMethod("ReadObject", Type.EmptyTypes);
                    var mi2 = us.MakeGenericMethod(i.FieldType);
                    i.SetValue(t, mi2.Invoke(this, null));
                }
            }
            return t;
        }

        public T[] ReadArray<T>(long addr, int count) where T : new() {
            Position = addr;
            return ReadArray<T>(count);
        }

        public T[] ReadArray<T>(int count) where T : new() {
            T[] t = new T[count];
            for (int i = 0; i < count; i++) {
                t[i] = ReadObject<T>();
            }
            return t;
        }

        public string ReadNullTerminatedString(long addr, Encoding encoding = null) {
            Position = addr;
            return ReadNullTerminatedString(encoding);
        }

        public string ReadNullTerminatedString(Encoding encoding = null) {
            List<byte> bytes = new List<byte>();
            byte b;
            while ((b = ReadByte()) != 0)
                bytes.Add(b);
            return encoding?.GetString(bytes.ToArray()) ?? Encoding.GetString(bytes.ToArray());
        }
    }
}
