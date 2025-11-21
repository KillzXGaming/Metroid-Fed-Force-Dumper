using AvaloniaToolbox.Core.Dialogs;
using AvaloniaToolbox.Core.IO;
using AvaloniaToolbox.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary.Files
{
    public class DataTreeNode : TreeNodeModel
    {
        private ChunkEntry _chunkEntry;

        public DataTreeNode(string name, Stream stream) : base(name)
        {
            Tag = stream;
            this.ContextMenus.Add(new MenuItemModel("Export", ExportAction));
        }
        public DataTreeNode(ChunkEntry chunkEntry) : base(chunkEntry.Type.ToString())
        {
            Tag = chunkEntry.Data;
            _chunkEntry = chunkEntry;
            this.ContextMenus.Add(new MenuItemModel("Export", ExportAction));
            this.ContextMenus.Add(new MenuItemModel("Replace", ReplaceAction));
        }
        public DataTreeNode(ChunkEntry chunkEntry, string name) : base(name)
        {
            Tag = chunkEntry.Data;
            _chunkEntry = chunkEntry;
            this.ContextMenus.Add(new MenuItemModel("Export", ExportAction));
            this.ContextMenus.Add(new MenuItemModel("Replace", ReplaceAction));
        }

        private async void ExportAction()
        {
            AppSaveFileDialog dlg = new AppSaveFileDialog();
            dlg.SuggestedFileName = this.Name;
            if (await dlg.ShowDialog())
            {
                var stream = this.Tag as Stream;
                if (stream != null && stream.CanRead)
                    await stream.SaveToFileAsync(dlg.FilePath);
                else
                    AppNotification.ShowError("Dict File", $"Failed to export stream");
            }
        }
        private async void ReplaceAction()
        {
            AppSaveFileDialog dlg = new AppSaveFileDialog();
            dlg.SuggestedFileName = this.Name;
            if (await dlg.ShowDialog())
            {
                _chunkEntry.Data = new MemoryStream(File.ReadAllBytes(dlg.FilePath));
                this.Tag = _chunkEntry.Data;
            }
        }
    }
}
