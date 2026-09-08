using System.ComponentModel;
using System.Xml.Serialization;
using Modding.Modules;

namespace TimerPlusMod
{
    /// <summary>
    /// The Logic Gate Plus block, as declared in LogicGatePlus.xml. The same shape
    /// as <see cref="TimerPlusModule"/>, and the same two rules apply: every
    /// attribute carries a [DefaultValue] or Besiege drops the block XML without
    /// a word, and the field initialiser beside it is not a substitute for the
    /// marker.
    /// </summary>
    [XmlRoot("LogicGatePlus")]
    public class LogicGatePlusModule : BlockModule
    {
        /// <summary>The block's own name, which the panel puts in its heading and
        /// the build's XML check holds to the block's &lt;Name&gt; element.</summary>
        [XmlAttribute("block")]
        [DefaultValue("Logic Gate Plus")]
        public string Block = "Logic Gate Plus";

        /// <summary>How many rows a newly placed block starts with. Only a newly
        /// placed one reads it: the count is a mapper control like everything
        /// else.</summary>
        [XmlAttribute("rows")]
        [DefaultValue(3)]
        public int Rows = 3;
    }
}
