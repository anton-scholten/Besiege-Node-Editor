using System.ComponentModel;
using System.Xml.Serialization;
using Modding.Modules;

namespace NodeEditorMod
{
    /// <summary>
    /// The Timer Plus block as TimerPlus.xml declares it; its table is built in
    /// code. Every attribute carries [DefaultValue]: `Serialization.Validate`
    /// silently drops a block XML missing a required one, and a field initialiser
    /// does not make one optional.
    /// </summary>
    [XmlRoot("TimerPlus")]
    public class TimerPlusModule : BlockModule
    {
        /// <summary>The block's name for the panel heading; the build checks it
        /// against &lt;Name&gt;.</summary>
        [XmlAttribute("block")]
        [DefaultValue("Timer Plus")]
        public string Block = "Timer Plus";

        /// <summary>Rows a newly placed block starts with, clamped to <see
        /// cref="TimerPlusBehaviour.MaxRows"/>.</summary>
        [XmlAttribute("rows")]
        [DefaultValue(1)]
        public int Rows = 1;
    }
}
