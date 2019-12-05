using System;
using System.IO;
using System.IO.Compression;
using FastRsync.Compression;
using FastRsync.Core;
using FastRsync.Delta;
using FastRsync.Signature;
using NUnit.Framework;

namespace FastRsync.Tests
{
    [TestFixture]
    public class GZipTests
    {
        [Test]
        [TestCase(120)]
        [TestCase(5 * 1024 * 1024)]
        public void GZip_CompressData(int dataLength)
        {
            // Arrange
            var data = new byte[dataLength];
            new Random().NextBytes(data);
            var srcStream = new MemoryStream(data);
            var destStream = new MemoryStream();

            // Act
            GZip.Compress(srcStream, destStream);

            // Assert
            destStream.Seek(0, SeekOrigin.Begin);
            var decompressedStream = new MemoryStream();
            using (var gz = new GZipStream(destStream, CompressionMode.Decompress))
            {
                gz.CopyTo(decompressedStream);
            }

            var dataOutput = decompressedStream.ToArray();
            Assert.AreEqual(data, dataOutput);
        }

        [Theory]
        [TestCase(120)]
        [TestCase(5 * 1024 * 1024)]
        public void GZip_CompressData_RsyncSignatureAndPatch(int dataLength)
        {
            // Arrange
            var dataBasis = new byte[dataLength];
            new Random().NextBytes(dataBasis);
            var basisStream = new MemoryStream(dataBasis);
            var basisStreamCompressed = new MemoryStream();
            var basisStreamCompressedSignature = new MemoryStream();

            var newFileStream = new MemoryStream();
            newFileStream.Write(dataBasis, 10, dataLength * 4 / 5);
            var newRandomData = new byte[dataLength * 2 / 5];
            new Random().NextBytes(newRandomData);
            newFileStream.Write(newRandomData, 0, newRandomData.Length);
            newFileStream.Seek(0, SeekOrigin.Begin);

            var newFileStreamCompressed = new MemoryStream();
            var deltaStream = new MemoryStream();
            var patchedCompressedStream = new MemoryStream();

            // Act
            GZip.Compress(basisStream, basisStreamCompressed);
            basisStreamCompressed.Seek(0, SeekOrigin.Begin);

            var signatureBuilder = new SignatureBuilder();
            signatureBuilder.Build(basisStreamCompressed, new SignatureWriter(basisStreamCompressedSignature));
            basisStreamCompressedSignature.Seek(0, SeekOrigin.Begin);

            GZip.Compress(newFileStream, newFileStreamCompressed);
            newFileStreamCompressed.Seek(0, SeekOrigin.Begin);

            var deltaBuilder = new DeltaBuilder();
            deltaBuilder.BuildDelta(newFileStreamCompressed, new SignatureReader(basisStreamCompressedSignature, null),
                new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(deltaStream)));
            deltaStream.Seek(0, SeekOrigin.Begin);

            var deltaApplier = new DeltaApplier
            {
                SkipHashCheck = true
            };
            var deltaReader = new BinaryDeltaReader(deltaStream, null);
            deltaApplier.Apply(basisStreamCompressed, deltaReader, patchedCompressedStream);
            deltaApplier.HashCheck(deltaReader, patchedCompressedStream);

            // Assert
            Assert.AreEqual(newFileStreamCompressed.ToArray(), patchedCompressedStream.ToArray());

            patchedCompressedStream.Seek(0, SeekOrigin.Begin);
            var decompressedStream = new MemoryStream();
            using (var gz = new GZipStream(patchedCompressedStream, CompressionMode.Decompress))
            {
                gz.CopyTo(decompressedStream);
            }

            var dataOutput = decompressedStream.ToArray();
            Assert.AreEqual(newFileStream.ToArray(), dataOutput);
        }
    }
}
