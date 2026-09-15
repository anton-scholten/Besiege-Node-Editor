namespace NodeEditorMod
{
    /// <summary>Hides the skin picker: the block has one mesh and one
    /// texture.</summary>
    public static class Skins
    {
        /// <summary>The key the game itself gives this control. Kept the same so a
        /// machine saved before this change still finds its stored value.</summary>
        private const string SkinKey = "_CurrentSkin";

        /// <summary>Hides the control rather than clearing `SkinCanBeChanged`,
        /// which left the block's icon on the loader's placeholder. Before the
        /// mapper first opens, or the game shows it once.</summary>
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
