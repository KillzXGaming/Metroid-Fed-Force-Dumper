using AvaloniaToolbox.Core.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary
{
    public interface IDataReader
    {
        void Read(FileReader reader, GameVersion gameVersion);
    }
}
