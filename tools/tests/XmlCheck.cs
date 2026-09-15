using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

/// <summary>
/// Checks the mod's XML, since Besiege silently leaves a bad block out of the
/// toolbar: it parses (no `--` in comments); blocks carry the elements the loader
/// insists on (BasePoint); a `modid`, if given, matches `Mod.xml`; module elements
/// supply every attribute without [DefaultValue].
/// </summary>
static class XmlCheck
{
    /// <summary>Elements Besiege refuses a block without.</summary>
    static readonly string[] BlockRequires =
    {
        "ID", "Name", "Mesh", "Texture", "Colliders", "BasePoint", "AddingPoints"
    };

    public static int Main(string[] args)
    {
        int bad = 0;
        string modId = ModId(args);
        Dictionary<string, string> resources = Resources(args);
        bad += Present(resources, args);
        Schema schema = Schema.Read(args);
        Dictionary<string, float[]> poses = IconPoses(args);
        int files = 0;
        int icons = 0;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].EndsWith(".cs") || args[i].EndsWith(".py"))
            {
                continue;                       // the module source and the mesh
            }                                   // tool, both read above
            files++;

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(args[i]);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("  " + args[i] + ": " + e.Message);
                bad++;
                continue;
            }

            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "Block")
            {
                continue;                       // Mod.xml and anything else
            }

            for (int r = 0; r < BlockRequires.Length; r++)
            {
                if (root.SelectSingleNode(BlockRequires[r]) == null)
                {
                    Console.Error.WriteLine("  " + args[i] + ": no <"
                                            + BlockRequires[r] + "> element");
                    bad++;
                }
            }

            // Meshes and textures named in one file have to be declared in the
            // other.
            bad += Wears(root, "Mesh", resources, args[i]);
            bad += Wears(root, "Texture", resources, args[i]);

            // The icon pose is also in the mesh tool, which renders the preview
            // from it: the two must agree.
            bad += Photographed(root, Pose(poses, args[i]), args[i], ref icons);

            bad += Modules(root, schema, modId, args[i]);
        }

        if (bad > 0)
        {
            return 1;
        }
        Console.WriteLine("XML check: " + files + " file(s) parse, block complete"
                          + schema.Summary()
                          + (icons > 0 ? ", icon posed as the mesh tool draws it" : "")
                          + ".");
        return 0;
    }

    static int Modules(XmlElement root, Schema schema, string modId, string path)
    {
        int bad = 0;
        XmlNode modules = root.SelectSingleNode("Modules");
        if (modules == null)
        {
            return 0;
        }
        for (int m = 0; m < modules.ChildNodes.Count; m++)
        {
            XmlElement module = modules.ChildNodes[m] as XmlElement;
            if (module == null)
            {
                continue;
            }
            bad += schema.Check(module, path);

            // Absent is legal: the loader then resolves the module against the mod
            // that owns this file.
            string owner = module.GetAttribute("modid");
            if (owner.Length > 0 && modId != null && owner != modId)
            {
                Console.Error.WriteLine("  " + path + ": <" + module.Name
                    + "> has modid=\"" + owner + "\", but Mod.xml says \""
                    + modId + "\"");
                bad++;
            }

            // The panel heading's name comes from here; checked against
            // &lt;Name&gt;.
            if (!module.HasAttribute("block"))
            {
                continue;
            }
            XmlNode named = root.SelectSingleNode("Name");
            string family = module.GetAttribute("block");
            if (named != null && family != named.InnerText.Trim())
            {
                Console.Error.WriteLine("  " + path + ": <" + module.Name
                    + "> has block=\"" + family + "\", but the block is called \""
                    + named.InnerText.Trim() + "\"");
                bad++;
            }
        }
        return bad;
    }

    /// <summary>The block's icon rotation against the mesh tool's pose.</summary>
    static int Photographed(XmlElement root, float[] want, string path, ref int seen)
    {
        if (want == null)
        {
            return 0;
        }
        XmlElement turn = root.SelectSingleNode("Icon/Rotation") as XmlElement;
        if (turn == null)
        {
            Console.Error.WriteLine("  " + path + ": no <Icon><Rotation>, but the mesh"
                                    + " tool poses the block for one");
            return 1;
        }
        string[] axes = { "x", "y", "z" };
        for (int k = 0; k < 3; k++)
        {
            float got;
            if (!float.TryParse(turn.GetAttribute(axes[k]), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out got)
                || Math.Abs(got - want[k]) > 0.001f)
            {
                Console.Error.WriteLine("  " + path + ": <Icon> turns " + axes[k] + "=\""
                    + turn.GetAttribute(axes[k]) + "\", but make-block-mesh.py draws it at "
                    + want[k].ToString(CultureInfo.InvariantCulture));
                return 1;
            }
        }
        seen++;
        return 0;
    }

    /// <summary>Every block's toolbar pose, read out of `make-block-mesh.py` by
    /// block name.</summary>
    static Dictionary<string, float[]> IconPoses(string[] args)
    {
        Dictionary<string, float[]> found = new Dictionary<string, float[]>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].EndsWith(".py"))
            {
                continue;
            }
            string source;
            try
            {
                source = File.ReadAllText(args[i]);
            }
            catch (Exception)
            {
                continue;               // an absent tool is not a broken block
            }
            string number = @"\s*(-?[0-9.]+)\s*";
            // A block's entry names it and then, a few lines down, poses it.
            foreach (Match m in Regex.Matches(source,
                "\"([A-Za-z0-9_]+)\"\\s*:\\s*\\{.*?\"icon\"\\s*:\\s*\\("
                + number + "," + number + "," + number + @"\)",
                RegexOptions.Singleline))
            {
                float[] pose = new float[3];
                bool read = true;
                for (int k = 0; k < 3; k++)
                {
                    if (!float.TryParse(m.Groups[k + 2].Value, NumberStyles.Float,
                                        CultureInfo.InvariantCulture, out pose[k]))
                    {
                        read = false;
                    }
                }
                if (read)
                {
                    found[m.Groups[1].Value] = pose;
                }
            }
        }
        return found;
    }

    /// <summary>The pose for the block in this file, matched by file name -- the
    /// XML beside the tool's entry for it is called the same thing.</summary>
    static float[] Pose(Dictionary<string, float[]> poses, string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        float[] want;
        return poses.TryGetValue(name, out want) ? want : null;
    }

    /// <summary>What the block wears is declared in Mod.xml, and is on
    /// disk.</summary>
    static int Wears(XmlElement root, string kind,
                     Dictionary<string, string> resources, string path)
    {
        XmlElement worn = root.SelectSingleNode(kind) as XmlElement;
        if (worn == null)
        {
            return 0;                       // the missing-element check has it
        }
        string name = worn.GetAttribute("name");
        string file;
        if (!resources.TryGetValue(kind + ":" + name, out file))
        {
            Console.Error.WriteLine("  " + path + ": <" + kind + " name=\"" + name
                + "\"> is not a " + kind + " Mod.xml declares");
            return 1;
        }
        // Mod.xml's paths are relative to the mod's Resources folder, not to the
        // manifest beside it.
        string here = Path.Combine(
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "Resources"),
            file.Replace('\\', Path.DirectorySeparatorChar));
        if (!File.Exists(here))
        {
            Console.Error.WriteLine("  " + path + ": " + kind + " \"" + name
                                    + "\" points at " + file + ", which is not there");
            return 1;
        }
        return 0;
    }

    /// <summary>Every declared resource is on disk, textures only code loads
    /// included.
    /// </summary>
    static int Present(Dictionary<string, string> resources, string[] args)
    {
        string root = null;
        for (int i = 0; i < args.Length && root == null; i++)
        {
            if (args[i].EndsWith("Mod.xml"))
            {
                root = Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(args[i])), "Resources");
            }
        }
        if (root == null)
        {
            return 0;
        }
        int bad = 0;
        foreach (KeyValuePair<string, string> one in resources)
        {
            string file = Path.Combine(
                root, one.Value.Replace('\\', Path.DirectorySeparatorChar));
            if (!File.Exists(file))
            {
                Console.Error.WriteLine("  Mod.xml declares " + one.Key
                                        + " at " + one.Value + ", which is not there");
                bad++;
            }
        }
        return bad;
    }

    /// <summary>Every mesh and texture Mod.xml declares, as kind:name -&gt; path.</summary>
    static Dictionary<string, string> Resources(string[] args)
    {
        Dictionary<string, string> found = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].EndsWith("Mod.xml"))
            {
                continue;
            }
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(args[i]);
                XmlNode list = doc.DocumentElement.SelectSingleNode("Resources");
                if (list == null)
                {
                    continue;
                }
                foreach (XmlNode node in list.ChildNodes)
                {
                    XmlElement res = node as XmlElement;
                    if (res == null || (res.Name != "Mesh" && res.Name != "Texture"))
                    {
                        continue;
                    }
                    found[res.Name + ":" + res.GetAttribute("name")] = res.GetAttribute("path");
                }
            }
            catch (Exception)
            {
                // The parse check above reports it.
            }
        }
        return found;
    }

    /// <summary>The mod's own ID, or null before the game has written one.</summary>
    static string ModId(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].EndsWith("Mod.xml"))
            {
                continue;
            }
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(args[i]);
                XmlNode id = doc.DocumentElement.SelectSingleNode("ID");
                if (id != null && id.InnerText.Length > 0)
                {
                    return id.InnerText;
                }
            }
            catch (Exception)
            {
                // The parse error is reported by the main loop.
            }
        }
        return null;
    }
}


/// <summary>Besiege's required-attribute rule, read off the module source's
/// `[Xml*]` and `[DefaultValue]` markers. A shallow parse: elements it finds no
/// class for are reported, not checked.</summary>
class Schema
{
    /// <summary>Class name -&gt; attributes that class requires.</summary>
    readonly Dictionary<string, List<string>> required =
        new Dictionary<string, List<string>>();

    /// <summary>XML element name -&gt; the class it deserialises into.</summary>
    readonly Dictionary<string, string> elements = new Dictionary<string, string>();

    bool loaded;
    int checkedElements;

    public static Schema Read(string[] args)
    {
        Schema s = new Schema();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].EndsWith(".cs"))
            {
                continue;
            }
            try
            {
                s.ReadSource(File.ReadAllLines(args[i]));
                s.loaded = true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("  " + args[i] + ": " + e.Message);
            }
        }
        return s;
    }

    void ReadSource(string[] lines)
    {
        string cls = null;
        string root = null;
        List<string> pending = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("///"))
            {
                continue;                       // comments never break a run
            }

            if (line.StartsWith("["))
            {
                pending.Add(line);
                continue;
            }

            Match c = Regex.Match(line, @"^(?:public\s+)?class\s+(\w+)");
            if (c.Success)
            {
                cls = c.Groups[1].Value;
                required[cls] = new List<string>();
                // [XmlRoot("Name")] on the class says which element it is.
                root = null;
                for (int a = 0; a < pending.Count; a++)
                {
                    if (pending[a].StartsWith("[XmlRoot"))
                    {
                        root = Quoted(pending[a]);
                    }
                }
                if (root != null)
                {
                    elements[root] = cls;
                }
                pending.Clear();
                continue;
            }

            Match f = Regex.Match(line, @"^public\s+(\S+)\s+(\w+)\s*[=;]");
            if (f.Success && cls != null)
            {
                Field(cls, f.Groups[1].Value, pending);
            }
            pending.Clear();
        }
    }

    void Field(string cls, string type, List<string> attributes)
    {
        string xmlAttribute = null, itemName = null, itemType = null;
        bool optional = false;

        for (int i = 0; i < attributes.Count; i++)
        {
            string a = attributes[i];
            if (a.StartsWith("[DefaultValue"))
            {
                optional = true;
            }
            else if (a.StartsWith("[XmlAttribute"))
            {
                xmlAttribute = Quoted(a);
            }
            else if (a.StartsWith("[XmlArrayItem") || a.StartsWith("[XmlElement"))
            {
                itemName = Quoted(a);
                Match t = Regex.Match(a, @"typeof\(\s*([\w\.]+)\s*\)");
                itemType = t.Success ? t.Groups[1].Value : null;
            }
        }

        if (xmlAttribute != null && !optional)
        {
            required[cls].Add(xmlAttribute);
        }
        if (itemName != null)
        {
            elements[itemName] = itemType != null ? itemType : type.Replace("[]", "").Trim();
        }
    }

    /// <summary>The first double-quoted string in an attribute, or null.</summary>
    static string Quoted(string attribute)
    {
        Match m = Regex.Match(attribute, "\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Checks one module element and everything under it, and returns how
    /// many required attributes were missing.</summary>
    public int Check(XmlElement module, string path)
    {
        return loaded ? Walk(module, path) : 0;
    }

    int Walk(XmlElement element, string path)
    {
        int bad = 0;
        string cls;
        List<string> want;
        if (elements.TryGetValue(element.Name, out cls)
            && required.TryGetValue(cls, out want))
        {
            checkedElements++;
            for (int i = 0; i < want.Count; i++)
            {
                if (!element.HasAttribute(want[i]))
                {
                    Console.Error.WriteLine("  " + path + ": <" + element.Name
                        + "> must have " + want[i] + " attribute! (" + cls
                        + "." + want[i] + " has no [DefaultValue])");
                    bad++;
                }
            }
        }
        for (int i = 0; i < element.ChildNodes.Count; i++)
        {
            XmlElement child = element.ChildNodes[i] as XmlElement;
            if (child != null)
            {
                bad += Walk(child, path);
            }
        }
        return bad;
    }

    public string Summary()
    {
        if (!loaded)
        {
            return "";
        }
        return ", " + checkedElements + " module element(s) checked against "
             + required.Count + " module class(es)";
    }
}
