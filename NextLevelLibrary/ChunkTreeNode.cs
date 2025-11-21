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
    public class ChunkTreeNode : TreeNodeModel
    {
        private ChunkEntry _chunkEntry;
        private bool _childrenLoaded = false;

        public ChunkTreeNode(ChunkEntry chunkEntry) : base(chunkEntry.Type.ToString())
        {
            Tag = chunkEntry.Data;
            _chunkEntry = chunkEntry;
            this.ContextMenus.Add(new MenuItemModel("Export", ExportAction));
            this.ContextMenus.Add(new MenuItemModel("Replace", ReplaceAction));

            if (_chunkEntry.HasChildren && _chunkEntry.Size > 0)
            {
                // Dummy node. We don't get Children instances unless needed for better performance
                this.AddChild(new TreeNodeModel("Dummy"));
            }
        }

        public override void OnExpanded()
        {
            base.OnExpanded();
            if (!_childrenLoaded)
            {
                _childrenLoaded = true;
                this.Children.Clear();
                foreach (var child in _chunkEntry.Children)
                    if (!child.IsDebug)
                        AddChild(new ChunkTreeNode(child));
            }
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
