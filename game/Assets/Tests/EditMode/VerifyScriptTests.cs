using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The repo's verify script (4.15, tools/verify.sh) runs the PlayMode tests in class batches, never all in one process.
    /// A test class left out of every batch would never run, so this reads the script's batches and every PlayMode test
    /// class (an outermost class whose body holds a [Test], [UnityTest], [TestCase] or [TestCaseSource]; comments ignored)
    /// and wants each class in exactly one batch, and every batched class to exist.
    /// </summary>
    public class VerifyScriptTests
    {
        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static string ScriptPath => Path.Combine(RepoRoot, "tools", "verify.sh");

        private static string PlayModeTests => Path.Combine(Application.dataPath, "Tests", "PlayMode");

        /// <summary>The script's batches (the BATCHES=( ... ) lines, with ${P} expanded; commented lines skipped), as full class names.</summary>
        public static List<List<string>> Batches(string script)
        {
            Match prefix = Regex.Match(script, "^P=\"([^\"]*)\"", RegexOptions.Multiline);
            string p = prefix.Success ? prefix.Groups[1].Value : "";
            Match block = Regex.Match(script, @"^BATCHES=\((.*?)^\)", RegexOptions.Multiline | RegexOptions.Singleline);
            Assert.IsTrue(block.Success, "verify.sh has a BATCHES=( ... ) block");
            var batches = new List<List<string>>();
            foreach (string raw in block.Groups[1].Value.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#")) continue;
                foreach (Match quoted in Regex.Matches(line, "\"([^\"]*)\""))
                    batches.Add(quoted.Groups[1].Value.Replace("${P}", p).Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList());
            }
            return batches;
        }

        /// <summary>The source with comments blanked out (line and block comments; string literals are kept as they are).</summary>
        public static string StripComments(string text) =>
            Regex.Replace(text, "\"(?:\\\\.|[^\"\\\\\\n])*\"|/\\*.*?\\*/|//[^\\n]*",
                m => m.Value.StartsWith("/") ? Regex.Replace(m.Value, "[^\\n]", " ") : m.Value, RegexOptions.Singleline);

        private static readonly Regex ClassDecl = new Regex(
            @"^[ \t]*(?:(?:public|internal|private|protected|sealed|abstract|static|partial|unsafe|new)\s+)*class\s+(\w+)",
            RegexOptions.Multiline);

        private static readonly Regex TestAttribute = new Regex(@"\[\s*(?:\w+(?:\([^\]]*?\))?\s*,\s*)*(?:Test|UnityTest|TestCase|TestCaseSource)\b");

        /// <summary>
        /// Every test class (namespace-qualified) in some C# source: an outermost class (any indent and modifiers, in a block
        /// or file-scoped namespace, or none) whose body, nested classes included, holds a [Test], [UnityTest], [TestCase] or
        /// [TestCaseSource]. Commented-out code counts for nothing.
        /// </summary>
        public static List<string> TestClassesIn(string source)
        {
            string text = StripComments(source);
            var classes = new List<string>();
            Match ns = Regex.Match(text, @"^[ \t]*namespace\s+([\w.]+)\s*(;|\{)?", RegexOptions.Multiline);
            int outer = ns.Success && ns.Groups[2].Value == "{" ? 1 : 0;
            int depth = 0, at = 0;
            foreach (Match decl in ClassDecl.Matches(text))
            {
                for (; at < decl.Index; at++)
                {
                    if (text[at] == '{') depth++;
                    else if (text[at] == '}') depth--;
                }
                if (depth != outer) continue; // a nested class: its tests count for the outer one
                int open = text.IndexOf('{', decl.Index);
                if (open < 0) continue;
                int d = 0, close = open;
                for (; close < text.Length; close++)
                {
                    if (text[close] == '{') d++;
                    else if (text[close] == '}' && --d == 0) break;
                }
                string body = text.Substring(open, Math.Min(close, text.Length - 1) - open + 1);
                if (!TestAttribute.IsMatch(body)) continue;
                string name = decl.Groups[1].Value;
                classes.Add(ns.Success ? ns.Groups[1].Value + "." + name : name);
            }
            return classes;
        }

        /// <summary>Every PlayMode test class (namespace-qualified) in a folder of test sources.</summary>
        public static List<string> TestClasses(string folder)
        {
            var classes = new List<string>();
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                classes.AddRange(TestClassesIn(File.ReadAllText(file)));
            return classes;
        }

        [Test]
        public void EveryPlayModeTestClass_IsInExactlyOneBatchOfTheVerifyScript()
        {
            Assert.IsTrue(File.Exists(ScriptPath), $"{ScriptPath} is missing");
            List<List<string>> batches = Batches(File.ReadAllText(ScriptPath));
            Assert.GreaterOrEqual(batches.Count, 2, "the PlayMode tests run in several batches");
            List<string> batched = batches.SelectMany(b => b).ToList();
            List<string> classes = TestClasses(PlayModeTests);
            Assert.Greater(classes.Count, 10, "the PlayMode test classes were found");

            var missing = classes.Where(c => !batched.Contains(c)).ToList();
            Assert.IsEmpty(missing, $"PlayMode test classes in no batch of tools/verify.sh (add them to BATCHES): {string.Join(", ", missing)}");
            var twice = batched.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(twice, $"Classes in more than one batch: {string.Join(", ", twice)}");
            var gone = batched.Where(c => !classes.Contains(c)).ToList();
            Assert.IsEmpty(gone, $"Batched classes that are not PlayMode test classes: {string.Join(", ", gone)}");
        }

        [Test]
        public void TestClasses_AreFound_InAnyLayout_AndCommentedOutOnesAreNot()
        {
            const string blockNamespace = @"namespace A.B
{
    public sealed partial class One : Base
    {
        [UnityTest] public IEnumerator Walks() { var s = $""{1} // not a comment""; yield return null; }
        private class Helper { }
        [Test, Timeout(5)] public void Later() { }
    }
    internal static class NoTests { public static int X() => 1; }
    // public class Gone { [Test] public void T() { } }
    /* public class AlsoGone
    {
        [Test] public void T() { }
    } */
public class Flush { [TestCase(1)] public void T(int i) { } }
    public class TimedFirst { [Timeout(5), UnityTest] public IEnumerator T() { yield return null; } }
}";
            CollectionAssert.AreEqual(new[] { "A.B.One", "A.B.Flush", "A.B.TimedFirst" }, TestClassesIn(blockNamespace));
            const string fileScoped = "namespace C;\n\nclass Two\n{\n\t[Test]\n\tpublic void T() { }\n}\n";
            CollectionAssert.AreEqual(new[] { "C.Two" }, TestClassesIn(fileScoped));
        }

        [Test]
        public void TheCheck_ReadsTheBatches_AndSkipsCommentedOnes()
        {
            const string script = "P=\"A.\"\nBATCHES=(\n\"${P}One;${P}Two\"\n# \"${P}Old\"\n\"B.Three\"\n)\n";
            List<List<string>> batches = Batches(script);
            Assert.AreEqual(2, batches.Count);
            CollectionAssert.AreEqual(new[] { "A.One", "A.Two" }, batches[0]);
            CollectionAssert.AreEqual(new[] { "B.Three" }, batches[1]);
        }
    }
}
