using System.ComponentModel;
using System.Xml.Serialization;
using Modding.Modules;

namespace NodeEditorMod
{
    /// <summary>
    /// The Timer Plus block, as declared in TimerPlus.xml.
    ///
    /// There is almost nothing in it. What the block does is its table, and a
    /// table is not something block XML can declare: every row is a set of mapper
    /// controls, registered in code because their number is fixed and their values
    /// belong to the machine rather than to the mod.
    ///
    /// The two rules that matter here, both of which cost a mod its blocks when
    /// they were got wrong elsewhere:
    ///
    /// * `Serialization.Validate` treats an [Xml*] member **without** a
    ///   [DefaultValue] as required, and drops the whole block XML -- silently,
    ///   the block simply never reaching the toolbar -- if the element omits it.
    ///   So every attribute here carries one.
    /// * A C# field initialiser is not a substitute for the marker. The
    ///   initialiser is what the value becomes; [DefaultValue] is what makes the
    ///   attribute optional. Both are wanted.
    /// </summary>
    [XmlRoot("TimerPlus")]
    public class TimerPlusModule : BlockModule
    {
        /// <summary>
        /// The block's own name, which the panel puts in its heading. The modding
        /// API gives a behaviour no name for its block, so it is stated here and
        /// the build's XML check holds it to the block's &lt;Name&gt; element.
        /// </summary>
        [XmlAttribute("block")]
        [DefaultValue("Timer Plus")]
        public string Block = "Timer Plus";

        /// <summary>
        /// How many rows a newly placed block starts with.
        ///
        /// Only a newly placed block reads this: the row count is a mapper control
        /// like everything else, so a block already sitting in a machine keeps
        /// whatever it was set to. Clamped to
        /// <see cref="TimerPlusBehaviour.MaxRows"/> on the way in.
        /// </summary>
        [XmlAttribute("rows")]
        [DefaultValue(3)]
        public int Rows = 3;
    }
}
