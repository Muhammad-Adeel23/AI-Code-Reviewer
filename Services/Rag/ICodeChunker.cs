using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public record CodeChunk(int Index, int StartLine, int EndLine, string Content, List<string> Symbols);

    public interface ICodeChunker
    {
        bool IsIndexable(string path, long sizeBytes);
        List<CodeChunk> Chunk(string filePath, string content);
    }
}
