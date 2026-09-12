using System;
using System.Collections.Generic;
using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Puts generated blocks into the machine being built, as a selection the
    /// player can then move.
    ///
    /// This is Besiege's own additive load, step for step:
    /// `MachineFileBrowserController.LoadAdditive` is what the load screen's "add
    /// to machine" button runs, and every member it uses is public. Doing the same
    /// thing means joints, clusters, undo and the selection tool all behave as they
    /// do for a machine loaded from a file, rather than as they do for blocks a mod
    /// invented -- and one undo takes the whole conversion away again.
    ///
    /// No file is written and nothing is parsed: the blocks go straight in as
    /// `BlockInfo`, which is what a `.bsg` would have been read into anyway.
    /// </summary>
    public static class Drop
    {
        /// <summary>Adds the blocks and leaves them selected. Returns how many
        /// arrived.</summary>
        public static int Into(List<BlockInfo> blocks, Machine machine)
        {
            if (blocks == null || blocks.Count == 0)
            {
                return 0;
            }

            bool merging = StatMaster.mergeSurfaceTypesOnDeselect;
            Dictionary<Guid, BlockBehaviour> made = null;
            List<UndoAction> undo = new List<UndoAction>();

            machine.isLoadingInfo = true;
            StatMaster.mergeSurfaceTypesOnDeselect = false;
            // What tells the rest of the game that these blocks are arriving as a
            // copy rather than being built one at a time.
            BlockSelectionTool.Duplicating = true;
            try
            {
                // The third argument is `ref`, not `out`, so the list has to exist
                // before the call. It reads as `out` and does not compile as one.
                machine.AddBlocksFromInfo(blocks, out made, ref undo);
                Select(machine, made, undo);
            }
            finally
            {
                BlockSelectionTool.Duplicating = false;
                StatMaster.mergeSurfaceTypesOnDeselect = merging;
                machine.isLoadingInfo = false;
            }
            return made == null ? 0 : made.Count;
        }

        /// <summary>
        /// Hands the new blocks to the selection tool, so the player is holding
        /// them and can put them where they want. The move tool is chosen for the
        /// same reason the load screen chooses it: a selection nobody can drag is
        /// not one worth making.
        /// </summary>
        private static void Select(Machine machine,
                                   Dictionary<Guid, BlockBehaviour> made,
                                   List<UndoAction> undo)
        {
            if (made == null || made.Count == 0)
            {
                return;
            }
            AdvancedBlockEditor editor = AdvancedBlockEditor.Instance;
            if (editor == null || editor.selectionController == null)
            {
                // Without the editor the blocks are still in the machine, which is
                // most of what was asked for.
                Log.Warn("the timers were added but could not be selected: "
                         + "the block editor is not up.");
                return;
            }

            BlockSelectionTool picker = editor.selectionController;
            picker.DeselectAll(true, true);
            editor.SetActiveTool(StatMaster.Tool.Translate);
            if (undo != null && undo.Count > 0 && machine.UndoSystem != null)
            {
                machine.UndoSystem.AddActions(undo);
            }

            picker.Select(new List<BlockBehaviour>(made.Values), true, true);

            AddPiece hammer = AddPiece.Instance;
            if (hammer != null)
            {
                if (picker.LastBlock != null)
                {
                    Transform last = picker.LastBlock.transform;
                    hammer.SingleHammerAnimate(last.position, last.position, last.forward);
                }
                hammer.UpdateMiddleOfObject(true);
            }
            if (machine.onBatchOperationComplete != null)
            {
                machine.onBatchOperationComplete();
            }
        }
    }
}
