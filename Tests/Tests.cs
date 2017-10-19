// Copyright (c) 2017 Katy Coe - http://www.djkaty.com - https://github.com/djkaty/Bin2Object/

using System;
using System.IO;
using NUnit.Framework;
using NoisyCowStudios.Bin2Object;

namespace Tests
{
    // Stop the "this item is never used" compiler spam
#pragma warning disable CS0649
    class TestObject
    {
        public int a;
        public short b;
        // No String attribute specified - use null-terminated by default
        public string c;
        public byte d;
    }

    class TestObjectWithArrays
    {
        public int numberOfItems;
        [ArrayLength(FieldName = "numberOfItems")]
        public ushort[] itemArray;
        [ArrayLength(FixedSize = 5)]
        public byte[] fiveItems;
    }

    class TestObjectWithStrings
    {
        [String(FixedSize = 8)]
        public string eightCharString;
        [String(FixedSize = 4)]
        public string fourCharString;
        [String(IsNullTerminated = true)]
        public string nullTerminatedString;
    }

    class TestObjectWithVersioning
    {
        public int allVersionsItem;
        [Version(Min = 2)]
        public string version2Item;
        [Version(Max = 1)]
        public string version1Item;
        [Version(Min = 1, Max = 2)]
        [String(IsNullTerminated = true)] // adding a 2nd attribute to prove it works
        public string version1And2Item;
    }
#pragma warning restore CS0649

    [TestFixture]
    class Tests
    {
        [Test]
        public void TestPrimitives([Values(Endianness.Little, Endianness.Big)] Endianness endianness) {
            var testData = new byte[] {0x04, 0x03, 0x02, 0x01, 0xFF, 0xFF, 0xFF, 0xFF};
            bool bigEndian = endianness == Endianness.Big;

            using (var stream = new MemoryStream(testData))
            using (var reader = new BinaryObjectReader(stream, endianness)) {
                // Raw bytes
                reader.Position = 0;

                var bytes = reader.ReadBytes(8);
                Assert.AreEqual(8, bytes.Length);
                for (var i = 0; i < bytes.Length; i++)
                    if (bigEndian)
                        Assert.AreEqual(testData[i], bytes[bytes.Length - i - 1]);
                    else
                        Assert.AreEqual(testData[i], bytes[i]);

                // Primitives with endianness
                reader.Position = 0;

                try {
                    Assert.AreEqual(bigEndian ? 0x04030201FFFFFFFF : 0xFFFFFFFF01020304, reader.ReadInt64());
                }
                catch (OverflowException) { }
                reader.Position -= 8;
                try {
                    Assert.AreEqual(bigEndian ? 0x04030201FFFFFFFF : 0xFFFFFFFF01020304, reader.ReadUInt64());
                }
                catch (OverflowException) { }
                reader.Position -= 8;
                Assert.AreEqual(bigEndian ? 0x04030201 : 0x01020304, reader.ReadInt32());
                reader.Position -= 4;
                Assert.AreEqual(bigEndian ? 0x04030201 : 0x01020304, reader.ReadUInt32());
                reader.Position -= 4;
                Assert.AreEqual(bigEndian ? 0x0403 : 0x0304, reader.ReadInt16());
                reader.Position -= 2;
                Assert.AreEqual(bigEndian ? 0x0403 : 0x0304, reader.ReadUInt16());

                // Signedness
                reader.Position = 4;

                Assert.AreEqual(-1, reader.ReadInt32());
                reader.Position -= 4;
                Assert.AreEqual(0xFFFFFFFF, reader.ReadUInt32());

                reader.Position = 6;

                Assert.AreEqual(-1, reader.ReadInt16());
                reader.Position -= 2;
                Assert.AreEqual(0xFFFF, reader.ReadUInt16());
            }
        }

        [Test]
        public void TestString() {
            var testData = new byte[] {0x41, 0x42, 0x43, 0x44, 0x00}; // ABCD\0

            using (var stream = new MemoryStream(testData))
            using (var reader = new BinaryObjectReader(stream)) {
                var str = reader.ReadNullTerminatedString();
                Assert.AreEqual("ABCD", str);
            }
        }

        [Test]
        public void TestObject([Values(Endianness.Little, Endianness.Big)] Endianness endianness) {
            byte[] testData;

            if (endianness == Endianness.Little)
                testData = new byte[] {0x04, 0x03, 0x02, 0x01, 0x34, 0x12, 0x41, 0x42, 0x43, 0x44, 0x00, 0x99};
            else {
                testData = new byte[] {0x01, 0x02, 0x03, 0x04, 0x12, 0x34, 0x41, 0x42, 0x43, 0x44, 0x00, 0x99};
            }

            using (var stream = new MemoryStream(testData))
            using (var reader = new BinaryObjectReader(stream, endianness)) {
                var obj = reader.ReadObject<TestObject>();

                Assert.AreEqual(0x01020304, obj.a);
                Assert.AreEqual(0x1234, obj.b);
                Assert.That("ABCD" == obj.c);
                Assert.AreEqual(0x99, obj.d);
            }
        }

        [Test]
        public void TestArray() {
            var testData = new byte[] {0x34, 0x12, 0x78, 0x56, 0xFF, 0xFF};

            using (var stream = new MemoryStream(testData))
            using (var reader = new BinaryObjectReader(stream)) {
                var arr = reader.ReadArray<short>(3);

                Assert.AreEqual(3, arr.Length);
                Assert.AreEqual(0x1234, arr[0]);
                Assert.AreEqual(0x5678, arr[1]);
                Assert.AreEqual(-1, arr[2]);
            }
        }

        [Test]
        public void TestArrayOfObject([Values(Endianness.Little, Endianness.Big)] Endianness endianness) {
            byte[] testData;

            if (endianness == Endianness.Little)
                testData = new byte[] {
                    0x04, 0x03, 0x02, 0x01, 0x34, 0x12, 0x41, 0x42, 0x43, 0x44, 0x00, 0x99,
                    0x05, 0x03, 0x02, 0x01, 0x35, 0x12, 0x41, 0x42, 0x43, 0x45, 0x00, 0x9A
                };
            else {
                testData = new byte[] {
                    0x01, 0x02, 0x03, 0x04, 0x12, 0x34, 0x41, 0x42, 0x43, 0x44, 0x00, 0x99,
                    0x01, 0x02, 0x03, 0x05, 0x12, 0x35, 0x41, 0x42, 0x43, 0x45, 0x00, 0x9A,
                };
            }

            using (var stream = new MemoryStream(testData))
            using (var reader = new BinaryObjectReader(stream, endianness)) {
                var objArr = reader.ReadArray<TestObject>(2);

                Assert.AreEqual(2, objArr.Length);

                Assert.AreEqual(0x01020304, objArr[0].a);
                Assert.AreEqual(0x1234, objArr[0].b);
                Assert.That("ABCD" == objArr[0].c);
                Assert.AreEqual(0x99, objArr[0].d);

                Assert.AreEqual(0x01020305, objArr[1].a);
                Assert.AreEqual(0x1235, objArr[1].b);
                Assert.That("ABCE" == objArr[1].c);
                Assert.AreEqual(0x9A, objArr[1].d);
            }
        }

        [Test]
        public void TestObjectWithArrays() {
            var testData = new byte[]
                {0x03, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E};

            using (var stream = new MemoryStream(testData))
            using (var reader = new BinaryObjectReader(stream)) {
                var obj = reader.ReadObject<TestObjectWithArrays>();

                Assert.AreEqual(3, obj.numberOfItems);
                Assert.AreEqual(3, obj.itemArray.Length);
                Assert.AreEqual(0x1234, obj.itemArray[0]);
                Assert.AreEqual(0x5678, obj.itemArray[1]);
                Assert.AreEqual(0x9ABC, obj.itemArray[2]);
                for (int i = 0; i < 5; i++)
                    Assert.AreEqual(i + 0x0A, obj.fiveItems[i]);
            }
        }

        [Test]
        public void TestObjectWithStrings() {
            var testData = new byte[]
                {0x41, 0x42, 0x43, 0x44, 0x45, 0x00, 0x00, 0x00, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x00};

            using (var stream = new MemoryStream(testData))
            using (var reader = new BinaryObjectReader(stream)) {
                var obj = reader.ReadObject<TestObjectWithStrings>();

                Assert.AreEqual("ABCDE", obj.eightCharString);
                Assert.AreEqual("FGHI", obj.fourCharString);
                Assert.AreEqual("JK", obj.nullTerminatedString);
            }
        }

        [Test]
        public void TestObjectWithVersioning() {
            var testData = new byte[]
                {0x34, 0x12, 0x00, 0x00, 0x41, 0x00, 0x42, 0x00, 0x43, 0x00};

            for (int v = 1; v <= 3; v++)
                using (var stream = new MemoryStream(testData))
                using (var reader = new BinaryObjectReader(stream)) {
                    reader.Version = v;
                    var obj = reader.ReadObject<TestObjectWithVersioning>();

                    Assert.AreEqual(0x1234, obj.allVersionsItem);
                    Assert.AreEqual((v == 1 ? "A" : null), obj.version1Item);
                    Assert.AreEqual((v >= 2 ? "A" : null), obj.version2Item);
                    Assert.AreEqual((v < 3 ? "B" : null), obj.version1And2Item);
                }
        }
    }
}
