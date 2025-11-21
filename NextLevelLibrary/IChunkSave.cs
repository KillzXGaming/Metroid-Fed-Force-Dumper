using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary
{
    public interface IChunkSave
    {
        void Save(ChunkEntry chunk);
    }
}
