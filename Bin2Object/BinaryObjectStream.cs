// Copyright (c) 2020 Katy Coe - http://www.djkaty.com - https://github.com/djkaty/Bin2Object/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NoisyCowStudios.Bin2Object
{
    // Combined BinaryObjectReader and BinaryObjectWriter that works with a MemoryStream
    public class BinaryObjectStream : MemoryStream {
        // The reader and writer
        private BinaryObjectReader Reader { get; }
        private BinaryObjectWriter Writer { get; }

        public Endianness Endianness {
            get => Reader.Endianness;
            set {
                Reader.Endianness = value;
                Writer.Endianness = value;
            }
        }

        public double Version {
            get => Reader.Version;
            set {
                Reader.Version = value;
                Writer.Version = value;
            }
        }

        public Encoding Encoding {
            get => Reader.Encoding;
            set {
                Reader.Encoding = value;
                Writer.Encoding = value;
            }
        }

        // Add a primitive mapping to the reader and writer
        public void AddPrimitiveMapping(Type objType, Type streamType) {
            Reader.PrimitiveMappings.Add(objType.Name, streamType);
            Writer.PrimitiveMappings.Add(objType, streamType);
        }

        // Create reader/writer
        public BinaryObjectStream(byte[] bytes, Endianness endianness = Endianness.Little, bool leaveOpen = false) : base(bytes) {
            Reader = new BinaryObjectReader(this, endianness, leaveOpen);
            Writer = new BinaryObjectWriter(this, endianness, leaveOpen);
        }
        public BinaryObjectStream(Endianness endianness = Endianness.Little, bool leaveOpen = false) {
            Reader = new BinaryObjectReader(this, endianness, leaveOpen);
            Writer = new BinaryObjectWriter(this, endianness, leaveOpen);
        }

        // Surrogate methods
        public byte[] ReadBytes(int count) => Reader.ReadBytes(count);
        public long ReadInt64() => Reader.ReadInt64();
        public ulong ReadUInt64() => Reader.ReadUInt64();
        public int ReadInt32() => Reader.ReadInt32();
        public uint ReadUInt32() => Reader.ReadUInt32();
        public short ReadInt16() => Reader.ReadInt16();
        public ushort ReadUInt16() => Reader.ReadUInt16();
        public bool ReadBoolean() => Reader.ReadBoolean();
        public float ReadSingle() => Reader.ReadSingle();
        public double ReadDouble() => Reader.ReadDouble();
        public new byte ReadByte() => Reader.ReadByte();

        public byte[] ReadBytes(long addr, int count) => Reader.ReadBytes(addr, count);
        public long ReadInt64(long addr) => Reader.ReadInt64(addr);
        public ulong ReadUInt64(long addr) => Reader.ReadUInt64(addr);
        public int ReadInt32(long addr) => Reader.ReadInt32(addr);
        public uint ReadUInt32(long addr) => Reader.ReadUInt32(addr);
        public short ReadInt16(long addr) => Reader.ReadInt16(addr);
        public ushort ReadUInt16(long addr) => Reader.ReadUInt16(addr);
        public byte ReadByte(long addr) => Reader.ReadByte(addr);
        public bool ReadBoolean(long addr) => Reader.ReadBoolean(addr);

        public T ReadObject<T>(long addr) where T : new() => Reader.ReadObject<T>(addr);
        public T ReadObject<T>() where T : new() => Reader.ReadObject<T>();

        public T[] ReadArray<T>(long addr, int count) where T : new() => Reader.ReadArray<T>(addr, count);
        public T[] ReadArray<T>(int count) where T : new() => Reader.ReadArray<T>(count);

        public string ReadNullTerminatedString(long addr, Encoding encoding = null) => Reader.ReadNullTerminatedString(addr, encoding);
        public string ReadNullTerminatedString(Encoding encoding = null) => Reader.ReadNullTerminatedString(encoding);
        public string ReadFixedLengthString(long addr, int length, Encoding encoding = null) => Reader.ReadFixedLengthString(addr, length, encoding);
        public string ReadFixedLengthString(int length, Encoding encoding = null) => Reader.ReadFixedLengthString(length, encoding);

        public void WriteEndianBytes(byte[] bytes) => Writer.WriteEndianBytes(bytes);
        public void Write(long int64) => Writer.Write(int64);
        public void Write(ulong uint64) => Writer.Write(uint64);
        public void Write(int int32) => Writer.Write(int32);
        public void Write(uint uint32) => Writer.Write(uint32);
        public void Write(short int16) => Writer.Write(int16);
        public void Write(ushort uint16) => Writer.Write(uint16);

        public void Write(long addr, byte[] bytes) => Writer.Write(addr, bytes);
        public void Write(long addr, long int64) => Writer.Write(addr, int64);
        public void Write(long addr, ulong uint64) => Writer.Write(addr, uint64);
        public void Write(long addr, int int32) => Writer.Write(addr, int32);
        public void Write(long addr, uint uint32) => Writer.Write(addr, uint32);
        public void Write(long addr, short int16) => Writer.Write(addr, int16);
        public void Write(long addr, ushort uint16) => Writer.Write(addr, uint16);
        public void Write(long addr, byte value) => Writer.Write(addr, value);
        public void Write(long addr, bool value) => Writer.Write(addr, value);
        
        public void WriteObject<T>(long addr, T obj) => Writer.WriteObject(addr, obj);
        public void WriteObject<T>(T obj) => Writer.WriteObject(obj);

        public void WriteArray<T>(long addr, T[] array) => Writer.WriteArray(addr, array);
        public void WriteArray<T>(T[] array) => Writer.WriteArray(array);

        public void WriteNullTerminatedString(long addr, string str, Encoding encoding = null) => Writer.WriteNullTerminatedString(addr, str, encoding);
        public void WriteNullTerminatedString(string str, Encoding encoding = null) => Writer.WriteNullTerminatedString(str, encoding);
        public void WriteFixedLengthString(long addr, string str, int size = -1, Encoding encoding = null) => Writer.WriteFixedLengthString(addr, str, size, encoding);
        public void WriteFixedLengthString(string str, int size = -1, Encoding encoding = null) => Writer.WriteFixedLengthString(str, size, encoding);
    }
}

