namespace TimerPlusMod
{
    /// <summary>
    /// Takes the skin picker out of the block's mapper. The block is its own mesh
    /// and its own texture with nothing to swap to, so the row is only ever an
    /// empty choice.
    /// </summary>
    public static class Skins
    {
        /// <summary>The key the game itself gives this control. Kept the same so a
        /// machine saved before this change still finds its stored value.</summary>
        private const string SkinKey = "_CurrentSkin";

        /// <summary>
        /// Not via `BlockPrefab.SkinCanBeChanged`: `BlockPrefab.SetIcons` reads
        /// that too and skips `SetPrefabIcons()` when it is false, which leaves the
        /// block showing the loader's placeholder texture in the block menu -- and
        /// clicking it repaints it with the placeholder again.
        ///
        /// The control is hidden instead. `GenericController.CreateContainers`
        /// skips any MapperType whose `DisplayInMapper` is false, but the control
        /// has to exist before the mapper first opens or the game builds it there
        /// and shows it once -- so this makes the same call `RefreshLists` would,
        /// after which `RefreshLists` takes its reuse path and leaves the flag
        /// alone.
        /// </summary>
        public static void Hide(BlockBehaviour block)
        {
            if (block == null)
            {
                return;
            }
            if (block.Visual == null)
            {
                BlockVisualController visuals = block.VisualController;
                if (visuals == null || visuals.Options == null || visuals.Options.Count == 0)
                {
                    return;
                }
                block.Visual = new MVisual(visuals,
                    visuals.Options.IndexOf(visuals.selectedSkin),
                    visuals.Options, SkinKey, null);
            }
            block.Visual.DisplayInMapper = false;
        }
    }
}
