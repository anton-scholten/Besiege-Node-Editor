using System.ComponentModel;
using System.Xml.Serialization;
using Modding.Modules;

namespace NodeEditorMod
{
    /// <summary>The Computer block as Computer.xml declares it; the same rules
    /// as <see cref="TimerPlusModule"/>.</summary>
    [XmlRoot("Computer")]
    public class ComputerModule : BlockModule
    {
        /// <summary>The block's own name, which the panel puts in its heading and
        /// the build's XML check holds to the block's &lt;Name&gt; element.</summary>
        [XmlAttribute("block")]
        [DefaultValue("Computer")]
        public string Block = "Computer";

        /// <summary>Rows a newly placed block starts with.</summary>
        [XmlAttribute("rows")]
        [DefaultValue(3)]
        public int Rows = 3;
    }
}
