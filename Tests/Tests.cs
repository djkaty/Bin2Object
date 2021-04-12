// Copyright (c) 2017-2019 Katy Coe - http://www.djkaty.com - https://github.com/djkaty/Bin2Object/

using System;
using System.IO;
using NUnit.Framework;
using NoisyCowStudios.Bin2Object;
using System.Linq;

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

    class TestMappedObject
    {
        [SkipWhenReading]
        public string c = "XYZ";

        public int e;   // Field does not exist in target

        public short a; // Map from a shorter primitive type
        public ulong d; // Map from a longer primitive type
        public short b;
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
        public string version2AndHigherItem;
        [Version(Max = 1)]
        public string version1Item;
        [Version(Min = 1, Max = 2)]
        [String(IsNullTerminated = true)] // adding a 2nd attribute to prove it works
        public string version1And2Item;
    }

    class TestObjectWithMultiVersioning
    {
        public byte a;
        [Version(Min = 1, Max = 2)]
        [Version(Min = 4)]
        public byte b;
        public byte c;
    }

    class TestObjectWithPrimitiveMapping
    {
        public int int1;
        public int int2;
        public bool bool1;
        public long long1;
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
        public void TestNullTerminatedString() {
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
                    Assert.AreEqual((v >= 2 ? "A" : null), obj.version2AndHigherItem);
                    Assert.AreEqual((v < 3 ? "B" : null), obj.version1And2Item);
                }
        }

        [Test]
        public void TestObjectWithMultiVersioning() {
            var testData = new byte[]
                {0x01, 0x02, 0x03};

            for (int v = 1; v <= 5; v++)
                using (var stream = new MemoryStream(testData))
                using (var reader = new BinaryObjectReader(stream)) {
                    reader.Version = v;
                    var obj = reader.ReadObject<TestObjectWithMultiVersioning>();

                    Assert.AreEqual(0x01, obj.a);
                    Assert.AreEqual(v != 3 ? 0x02 : 0x00, obj.b);
                    Assert.AreEqual(v != 3 ? 0x03 : 0x02, obj.c);
                }
        }

        [Test]
        public void TestMappedPrimitives() {
            var testData = new byte[]
                {0x01, 0x02, 0x01, 0x87, 0x65, 0x43, 0x21};

            using (var stream = new MemoryStream(testData))
            using (var reader = new BinaryObjectReader(stream)) {
                reader.PrimitiveMappings.Add(typeof(int).Name, typeof(byte));
                reader.PrimitiveMappings.Add(typeof(bool).Name, typeof(byte));
                reader.PrimitiveMappings.Add(typeof(long).Name, typeof(int));

                var obj = reader.ReadObject<TestObjectWithPrimitiveMapping>();

                Assert.AreEqual(obj.int1, 1);
                Assert.AreEqual(obj.int2, 2);
                Assert.AreEqual(obj.bool1, true);
                Assert.AreEqual(obj.long1, 0x21436587);
            }
        }

        [Test]
        public void TestMappedObject() {

            var testData = new byte[]
                { 0xCC, 0xCC, 0xCC, 0xCC, 0x02, 0x01, 0x99, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x34, 0x12 };

            using var stream = new MemoryStream(testData);
            using var reader = new BinaryObjectReader(stream);

            reader.ObjectMappings.Add(typeof(TestObject), typeof(TestMappedObject));

            var obj = reader.ReadObject<TestObject>();

            Assert.AreEqual(0x0102, obj.a);
            Assert.AreEqual(0x1234, obj.b);
            Assert.That("XYZ" == obj.c);
            Assert.AreEqual(0x99, obj.d);
        }

        [Test]
        public void TestWriterPrimitives() {
            var expected = new byte[]
                { 0x01, 0x02, 0x03,
                  0xef, 0xcd, 0xab, 0x89, 0x67, 0x45, 0x23, 0x01,
                  0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff,
                  0x78, 0x56, 0x34, 0x12, 0x98, 0xba, 0xdc, 0xfe,
                  0x34, 0x12, 0xdc, 0xfe, 0xcc, 0x01 };

            using var stream = new MemoryStream();
            using (var writer = new BinaryObjectWriter(stream)) {
                writer.WriteEndianBytes(new byte[] { 0x01, 0x02, 0x03 });
                writer.Write((long) 0x0123456789abcdef);
                writer.Write((ulong) 0xffeeddccbbaa9988);
                writer.Write((int) 0x12345678);
                writer.Write((uint) 0xfedcba98);
                writer.Write((short) 0x1234);
                writer.Write((ushort) 0xfedc);
                writer.Write((byte) 0xcc);
                writer.Write(true);
            }
            var bytes = stream.ToArray();
            Assert.That(Enumerable.SequenceEqual(bytes, expected));
        }

        [Test]
        public void TestWriterStrings() {
            var expected = new byte[]
                { 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x00,
                  0x77, 0x6f, 0x72, 0x6c, 0x64, 0x00,
                  0x74, 0x65, 0x73, 0x74,
                  0x74, 0x65, 0x73, 0x74, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            using var stream = new MemoryStream();
            using (var writer = new BinaryObjectWriter(stream)) {
                writer.WriteNullTerminatedString("hello");
                writer.WriteNullTerminatedString("world");
                writer.WriteFixedLengthString("test");
                writer.WriteFixedLengthString("test", 10);
            }
            var bytes = stream.ToArray();
            Assert.That(Enumerable.SequenceEqual(bytes, expected));
        }

        [Test]
        public void TestWriterObject([Values(Endianness.Little, Endianness.Big)] Endianness endianness) {
            var expected =
                endianness == Endianness.Little? new byte[]
                { 0x78, 0x56, 0x34, 0x12, 0x34, 0x12, 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x00, 0xff }

                : new byte[]
                { 0x12, 0x34, 0x56, 0x78, 0x12, 0x34, 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x00, 0xff };

            var obj = new TestObject {
                a = 0x12345678,
                b = 0x1234,
                c = "hello",
                d = 0xff
            };

            using var stream = new MemoryStream();
            using (var writer = new BinaryObjectWriter(stream, endianness)) {
                writer.WriteObject(obj);
            }
            var bytes = stream.ToArray();
            Assert.That(Enumerable.SequenceEqual(bytes, expected));
        }

        [Test]
        public void TestWriterArray() {
            var expected = new byte[] { 0x34, 0x12, 0x78, 0x56, 0xff, 0xff };

            var testData = new short[] { 0x1234, 0x5678, -1 };

            using var stream = new MemoryStream();
            using (var writer = new BinaryObjectWriter(stream)) {
                writer.WriteArray(testData);
            }
            var bytes = stream.ToArray();
            Assert.That(Enumerable.SequenceEqual(bytes, expected));
        }

        [Test]
        public void TestWriterArrayOfObject([Values(Endianness.Little, Endianness.Big)] Endianness endianness) {
            byte[] expected;

            if (endianness == Endianness.Little)
                expected = new byte[] {
                    0x04, 0x03, 0x02, 0x01, 0x34, 0x12, 0x41, 0x42, 0x43, 0x44, 0x00, 0x99,
                    0x05, 0x03, 0x02, 0x01, 0x35, 0x12, 0x41, 0x42, 0x43, 0x45, 0x00, 0x9A
                };
            else {
                expected = new byte[] {
                    0x01, 0x02, 0x03, 0x04, 0x12, 0x34, 0x41, 0x42, 0x43, 0x44, 0x00, 0x99,
                    0x01, 0x02, 0x03, 0x05, 0x12, 0x35, 0x41, 0x42, 0x43, 0x45, 0x00, 0x9A,
                };
            }

            var testData = new TestObject[] {
                new TestObject { a = 0x01020304, b = 0x1234, c = "ABCD", d = 0x99 },
                new TestObject { a = 0x01020305, b = 0x1235, c = "ABCE", d = 0x9A }
            };

            using var stream = new MemoryStream();
            using (var writer = new BinaryObjectWriter(stream, endianness)) {
                writer.WriteArray(testData);
            }
            var bytes = stream.ToArray();
            Assert.That(Enumerable.SequenceEqual(bytes, expected));
        }

        [Test]
        public void TestWriterObjectWithArrays() {
            var expected = new byte[]
                {0x03, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E};

            var testData = new TestObjectWithArrays {
                numberOfItems = 3,
                itemArray = new ushort[] { 0x1234, 0x5678, 0x9ABC },
                fiveItems = new byte[] { 0x0A, 0x0B, 0x0C, 0x0D, 0x0E }
            };

            using var stream = new MemoryStream();
            using (var writer = new BinaryObjectWriter(stream)) {
                writer.WriteObject(testData);
            }
            var bytes = stream.ToArray();
            Assert.That(Enumerable.SequenceEqual(bytes, expected));
        }

        [Test]
        public void TestWriterObjectWithStrings() {
            var expected = new byte[]
                {0x41, 0x42, 0x43, 0x44, 0x45, 0x00, 0x00, 0x00, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x00};

            var testData = new TestObjectWithStrings {
                eightCharString = "ABCDE",
                fourCharString = "FGHI",
                nullTerminatedString = "JK"
            };

            using var stream = new MemoryStream();
            using (var writer = new BinaryObjectWriter(stream)) {
                writer.WriteObject(testData);
            }
            var bytes = stream.ToArray();
            Assert.That(Enumerable.SequenceEqual(bytes, expected));
        }

        [Test]
        public void TestWriterObjectWithVersioning() {
            var expected = new byte[][] {
                new byte[] {0x34, 0x12, 0x00, 0x00, 0x41, 0x00, 0x43, 0x00 },
                new byte[] {0x34, 0x12, 0x00, 0x00, 0x42, 0x00, 0x43, 0x00 },
                new byte[] {0x34, 0x12, 0x00, 0x00, 0x42, 0x00 }
            };

            var testData = new TestObjectWithVersioning {
                allVersionsItem = 0x1234,
                version1Item = "A",
                version2AndHigherItem = "B",
                version1And2Item = "C"
            };

            for (int v = 1; v <= 3; v++)
                using (var stream = new MemoryStream()) {
                    using (var writer = new BinaryObjectWriter(stream)) {
                        writer.Version = v;
                        writer.WriteObject(testData);
                    }
                    var bytes = stream.ToArray();
                    Assert.That(Enumerable.SequenceEqual(bytes, expected[v - 1]));
                }
        }

        [Test]
        public void TestWriterObjectWithMultiVersioning() {
            var expected = new byte[][] {
                new byte[] {0x01, 0x02, 0x03},
                new byte[] {0x01, 0x02, 0x03},
                new byte[] {0x01, 0x03},
                new byte[] {0x01, 0x02, 0x03},
                new byte[] {0x01, 0x02, 0x03},
            };

            var testData = new TestObjectWithMultiVersioning {
                a = 0x01,
                b = 0x02,
                c = 0x03
            };

            for (int v = 1; v <= 5; v++)
                using (var stream = new MemoryStream()) {
                    using (var writer = new BinaryObjectWriter(stream)) {
                        writer.Version = v;
                        writer.WriteObject(testData);
                    }
                    var bytes = stream.ToArray();
                    Assert.That(Enumerable.SequenceEqual(bytes, expected[v - 1]));
                }
        }

        [Test]
        public void TestWriterMappedPrimitives() {
            var expected = new byte[]
                {0x01, 0x02, 0x01, 0x87, 0x65, 0x43, 0x21};

            var testData = new TestObjectWithPrimitiveMapping {
                int1 = 1,
                int2 = 2,
                bool1 = true,
                long1 = 0x21436587
            };

            using var stream = new MemoryStream();
            using (var writer = new BinaryObjectWriter(stream)) {
                writer.PrimitiveMappings.Add(typeof(int), typeof(byte));
                writer.PrimitiveMappings.Add(typeof(bool), typeof(byte));
                writer.PrimitiveMappings.Add(typeof(long), typeof(int));

                writer.WriteObject(testData);
            }
            var bytes = stream.ToArray();
            Assert.That(Enumerable.SequenceEqual(bytes, expected));
        }
    }
}
