using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastRsync.Core;
using FastRsync.Diagnostics;
using FastRsync.Hash;

namespace FastRsync.Signature {
    public class SignatureBuilder {
        public const short MinimumChunkSize = 128;
        public const short DefaultChunkSize = 2048;
        public const short MaximumChunkSize = 31 * 1024;

        private short chunkSize;

        public SignatureBuilder() : this(SupportedAlgorithms.Hashing.Default(), SupportedAlgorithms.Checksum.Default()) {
        }

        public SignatureBuilder(IHashAlgorithm hashAlgorithm, IRollingChecksum rollingChecksumAlgorithm) {
            HashAlgorithm = hashAlgorithm;
            RollingChecksumAlgorithm = rollingChecksumAlgorithm;
            ChunkSize = DefaultChunkSize;
            ProgressReport = null;
        }

        public IProgress<ProgressReport> ProgressReport { get; set; }

        public IHashAlgorithm HashAlgorithm { get; set; }

        public IRollingChecksum RollingChecksumAlgorithm { get; set; }


        /// <summary>
        /// Number of iterations after which to report. This will give the caller a way to throttle the number
        /// of reports generated by the builder.
        ///
        /// Note: The default of -1 will keep sending reports after each iteration. 
        /// </summary>
        public long ProgressReportEvery { get; set; } = -1;


        public short ChunkSize {
            get => chunkSize;
            set {
                if (value < MinimumChunkSize)
                    throw new ArgumentException($"Chunk size cannot be less than {MinimumChunkSize}");
                if (value > MaximumChunkSize)
                    throw new ArgumentException($"Chunk size cannot be exceed {MaximumChunkSize}");
                chunkSize = value;
            }
        }

        public void Build(Stream baseDataStream, ISignatureWriter signatureWriter) {
            WriteMetadata(baseDataStream, signatureWriter);
            WriteChunkSignatures(baseDataStream, signatureWriter);
        }

        public async Task BuildAsync(Stream baseDataStream, ISignatureWriter signatureWriter, CancellationToken ct = default) {
            await WriteMetadataAsync(baseDataStream, signatureWriter, ct).ConfigureAwait(false);
            await WriteChunkSignaturesAsync(baseDataStream, signatureWriter, ct).ConfigureAwait(false);
        }

        private void WriteMetadata(Stream baseFileStream, ISignatureWriter signatureWriter) {
            ProgressReport?.Report(new ProgressReport {
                Operation = ProgressOperationType.HashingFile,
                CurrentPosition = 0,
                Total = baseFileStream.Length
            });

            baseFileStream.Seek(0, SeekOrigin.Begin);
            var baseFileVerificationHashAlgorithm = SupportedAlgorithms.Hashing.Md5();
            var baseFileHash = baseFileVerificationHashAlgorithm.ComputeHash(baseFileStream);

            signatureWriter.WriteMetadata(new SignatureMetadata {
                ChunkHashAlgorithm = HashAlgorithm.Name,
                RollingChecksumAlgorithm = RollingChecksumAlgorithm.Name,
                BaseFileHashAlgorithm = baseFileVerificationHashAlgorithm.Name,
                BaseFileHash = Convert.ToBase64String(baseFileHash)
            });

            ProgressReport?.Report(new ProgressReport {
                Operation = ProgressOperationType.HashingFile,
                CurrentPosition = baseFileStream.Length,
                Total = baseFileStream.Length
            });
        }

        private async Task WriteMetadataAsync(Stream baseFileStream, ISignatureWriter signatureWriter, CancellationToken ct = default) {

            if (ct.IsCancellationRequested) return;

            ProgressReport?.Report(new ProgressReport {
                Operation = ProgressOperationType.HashingFile,
                CurrentPosition = 0,
                Total = baseFileStream.Length
            });

            baseFileStream.Seek(0, SeekOrigin.Begin);
            var baseFileVerificationHashAlgorithm = SupportedAlgorithms.Hashing.Md5();
            var baseFileHash = await baseFileVerificationHashAlgorithm.ComputeHashAsync(baseFileStream).ConfigureAwait(false);

            await signatureWriter.WriteMetadataAsync(new SignatureMetadata {
                ChunkHashAlgorithm = HashAlgorithm.Name,
                RollingChecksumAlgorithm = RollingChecksumAlgorithm.Name,
                BaseFileHashAlgorithm = baseFileVerificationHashAlgorithm.Name,
                BaseFileHash = Convert.ToBase64String(baseFileHash)
            }).ConfigureAwait(false);

            ProgressReport?.Report(new ProgressReport {
                Operation = ProgressOperationType.HashingFile,
                CurrentPosition = baseFileStream.Length,
                Total = baseFileStream.Length
            });

        }

        private void WriteChunkSignatures(Stream baseFileStream, ISignatureWriter signatureWriter) {
            var checksumAlgorithm = RollingChecksumAlgorithm;
            var hashAlgorithm = HashAlgorithm;

            ProgressReport?.Report(new ProgressReport {
                Operation = ProgressOperationType.BuildingSignatures,
                CurrentPosition = 0,
                Total = baseFileStream.Length
            });
            baseFileStream.Seek(0, SeekOrigin.Begin);

            long progressIterationCount = 0;
            long start = 0;
            int read;
            var block = new byte[ChunkSize];
            while ((read = baseFileStream.Read(block, 0, block.Length)) > 0) {
                signatureWriter.WriteChunk(new ChunkSignature {
                    StartOffset = start,
                    Length = (short)read,
                    Hash = hashAlgorithm.ComputeHash(block, 0, read),
                    RollingChecksum = checksumAlgorithm.Calculate(block, 0, read)
                });

                start += read;
                progressIterationCount += 1;
                if (ProgressReportEvery != -1 && ProgressReportEvery < progressIterationCount) continue;

                ProgressReport?.Report(new ProgressReport {
                    Operation = ProgressOperationType.BuildingSignatures,
                    CurrentPosition = start,
                    Total = baseFileStream.Length
                });
                progressIterationCount = 0;

            }
        }

        private async Task WriteChunkSignaturesAsync(Stream baseFileStream, ISignatureWriter signatureWriter, CancellationToken ct = default) {

            if (ct.IsCancellationRequested) return;

            var checksumAlgorithm = RollingChecksumAlgorithm;
            var hashAlgorithm = HashAlgorithm;

            ProgressReport?.Report(new ProgressReport {
                Operation = ProgressOperationType.BuildingSignatures,
                CurrentPosition = 0,
                Total = baseFileStream.Length
            });
            baseFileStream.Seek(0, SeekOrigin.Begin);

            long progressIterationCount = 0;
            long start = 0;
            int read;
            var block = new byte[ChunkSize];
            while ((read = await baseFileStream.ReadAsync(block, 0, block.Length, ct).ConfigureAwait(false)) > 0) {

                if (ct.IsCancellationRequested) return;

                await signatureWriter.WriteChunkAsync(new ChunkSignature {
                    StartOffset = start,
                    Length = (short)read,
                    Hash = hashAlgorithm.ComputeHash(block, 0, read),
                    RollingChecksum = checksumAlgorithm.Calculate(block, 0, read)
                }).ConfigureAwait(false);

                start += read;
                progressIterationCount += 1;
                if (ProgressReportEvery != -1 && ProgressReportEvery < progressIterationCount) continue;

                ProgressReport?.Report(new ProgressReport {
                    Operation = ProgressOperationType.BuildingSignatures,
                    CurrentPosition = start,
                    Total = baseFileStream.Length
                });
                progressIterationCount = 0;
            }
        }
    }
}