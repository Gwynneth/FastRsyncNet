using System;
using System.IO;
using FastRsync.Core;
using FastRsync.Delta;
using FastRsync.Diagnostics;
using FastRsync.Signature;
using NSubstitute;
using NUnit.Framework;
using System.Security.Cryptography;

namespace FastRsync.Tests
{
    [TestFixture]
    public class PatchingBigFilesTests
    {
        [Test]
        [TestCase(@"c:\repos\FastRsyncNet\source\FastRsync.Tests\bin\Debug\net472\bigfile1.bin", @"c:\repos\FastRsyncNet\source\FastRsync.Tests\bin\Debug\net472\bigfile2.bin")]
        public void PatchingSyncXXHash_BigFile(string originalFileName, string newFileName)
        {
            try
            {
                // Arrange
                var (baseDataStream, baseSignatureStream, newDataStream) = PrepareTestData(originalFileName, newFileName);

                var progressReporter = Substitute.For<IProgress<ProgressReport>>();

                // Act
                var deltaStream = new MemoryStream();
                var deltaBuilder = new DeltaBuilder();
                deltaBuilder.BuildDelta(newDataStream, new SignatureReader(baseSignatureStream, null), new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(deltaStream)));
                deltaStream.Seek(0, SeekOrigin.Begin);

                var patchedDataStream = new FileStream(Path.GetTempFileName(), FileMode.CreateNew);
                var deltaApplier = new DeltaApplier();
                deltaApplier.Apply(baseDataStream, new BinaryDeltaReader(deltaStream, progressReporter), patchedDataStream);

                // Assert
                //CollectionAssert.AreEqual(newDataStream.ToArray(), patchedDataStream.ToArray());
                progressReporter.Received().Report(Arg.Any<ProgressReport>());
            }
            catch (Exception e)
            {
                Assert.Fail();
            }
        }


        public static (Stream baseDataStream, Stream baseSignatureStream, Stream newDataStream) PrepareTestData(string originalFileName, string newFileName)
        {
            var baseDataStream = new FileStream(originalFileName, FileMode.Open);
            var baseSignatureStream = new MemoryStream();

            var signatureBuilder = new SignatureBuilder();
            signatureBuilder.Build(baseDataStream, new SignatureWriter(baseSignatureStream));
            baseSignatureStream.Seek(0, SeekOrigin.Begin);

            var newDataStream = new FileStream(newFileName, FileMode.Open);
            return (baseDataStream, baseSignatureStream, newDataStream);
        }
    }
}
