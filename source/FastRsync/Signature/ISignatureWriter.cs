using System.Threading.Tasks;
using FastRsync.Core;

namespace FastRsync.Signature
{
    public interface ISignatureWriter
    {
        void WriteMetadata(SignatureMetadata metadata);
        Task WriteMetadataAsync(SignatureMetadata metadata);
        void WriteChunk(ChunkSignature signature);
        Task WriteChunkAsync(ChunkSignature signature);
    }
}