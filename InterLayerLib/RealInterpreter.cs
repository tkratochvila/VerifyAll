using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using InterLayerLib;

// Interpretor of realisability results for the user.
namespace RealInterpreter
{
    public class Coverage
    {
        enum InputCoverage { none, partial, full };
        enum OutputCoverage { contant, altered, periodic };
        Digraph g;
        Tuple<List<Variable>, List<Variable>> iov;
        Dictionary<Variable, InputCoverage> iCov;
        Dictionary<Variable, OutputCoverage> oCov;

        public Coverage()
        {
            iCov = new Dictionary<Variable, InputCoverage>();
            oCov = new Dictionary<Variable, OutputCoverage>();
        }

        public void initialise(string graphFileContent, string partFileContent)
        {
            iov = load_variables(partFileContent);
            refine(graphFileContent);
        }

        public void refine(string graphFileContent)
        {
            if (graphFileContent.IndexOf("Transition system") == -1)
            {
                DotGraph dg = new DotGraph(graphFileContent);
                g = new Digraph(dg);
            }
            else
            {
                TxtGraph tg = new TxtGraph(graphFileContent);
                g = new Digraph(tg);
            }
        }
        /// <summary>
        /// Load input and output variables
        /// </summary>
        /// <param name="partFile"></param>
        /// <returns></returns>
        public Tuple<List<Variable>, List<Variable>> load_variables(string partFile)
        {
            string varPattern = @"(?:.*\n)*.inputs\s+(?:(\S+)\s+)*(?:.*\n)*.outputs\s+(?:(\S+)\s*)*(?:.*\n)*";
            Regex varRegex = new Regex(varPattern);
            Match m = varRegex.Match(partFile);

            List<Variable> inputVars = new List<Variable>();
            foreach (var v in m.Groups[1].Captures)
            {
                inputVars.Add(new Variable(v.ToString(), true));
            }
            List<Variable> outputVars = new List<Variable>();
            foreach (var v in m.Groups[2].Captures)
            {
                outputVars.Add(new Variable(v.ToString(), false));
            }
            return new Tuple<List<Variable>, List<Variable>>(inputVars, outputVars);
        }

        InputCoverage choose_higher(InputCoverage first, InputCoverage second)
        {
            if (first == InputCoverage.full || second == InputCoverage.full)
                return InputCoverage.full;
            if (first == InputCoverage.partial || second == InputCoverage.partial)
                return InputCoverage.partial;
            return InputCoverage.none;
        }
        void add_input_cov(Variable v, InputCoverage ic)
        {
            if (iCov.ContainsKey(v))
                iCov[v] = choose_higher(iCov[v], ic);
            else
                iCov.Add(v, ic);
        }
        OutputCoverage choose_higher(OutputCoverage first, OutputCoverage second)
        {
            if (first == OutputCoverage.periodic || second == OutputCoverage.periodic)
                return OutputCoverage.periodic;
            if (first == OutputCoverage.altered || second == OutputCoverage.altered)
                return OutputCoverage.altered;
            return OutputCoverage.contant;
        }
        void add_output_cov(Variable v, OutputCoverage oc)
        {
            if (oCov.ContainsKey(v))
                oCov[v] = choose_higher(oCov[v], oc);
            else
                oCov.Add(v, oc);
        }

        void compute_input_coverage()
        {
            foreach (var v in iov.Item1)
            {
                string vName = v.name;
                bool negCovered = false;
                bool posCovered = false;
                HashSet<string> responses = new HashSet<string>();

                g.bfs_pass_edges((e) =>
                {
                    if (e.label.Contains(v.name))
                    {
                        if (e.label.Contains("!" + v.name))
                            negCovered = true;
                        else
                            posCovered = true;
                        responses.Add(e.label.Substring(e.label.IndexOf('U')));
                    }
                });

                if (posCovered && negCovered)
                {
                    add_input_cov(v, InputCoverage.full);
                    continue;
                }
                if ((posCovered || negCovered) && responses.Count > 1)
                    add_input_cov(v, InputCoverage.partial);
                else
                    add_input_cov(v, InputCoverage.none);
            }
        }

        void compute_output_coverage()
        {
            g.scc_decomposition();
            foreach (var v in iov.Item2)
            {
                bool determined = false;
                HashSet<int> positives = new HashSet<int>();
                HashSet<int> negatives = new HashSet<int>();

                for (int i = 0; i < g.edges.Count; i++)
                {
                    if (g.edges[i].contains_negative(v.name))
                        negatives.Add(i);
                    else if (g.edges[i].contains_positive(v.name))
                        positives.Add(i);
                }

                foreach (var dec in g.EdgeDecomposition)
                {
                    if (positives.Intersect(dec).Count() != 0 && negatives.Intersect(dec).Count() != 0)
                    {
                        add_output_cov(v, OutputCoverage.periodic);
                        determined = true;
                        break;
                    }
                }
                if (determined)
                    continue;

                var posReach = g.edge_reachability(positives);
                var negReach = g.edge_reachability(negatives);
                if (posReach.Intersect(negReach).Count() != 0)
                    add_output_cov(v, OutputCoverage.altered);
                else
                    add_output_cov(v, OutputCoverage.contant);
            }

        }


        public void compute()
        {
            compute_input_coverage();
            compute_output_coverage();
        }

        public string print_short_icov()
        {
            string output = "";
            bool trivial = true;
            foreach (var c in iCov)
            {
                output += c.Key.name + ": ";
                if (c.Value == InputCoverage.full)
                {
                    output += "F";
                    trivial = false;
                }
                if (c.Value == InputCoverage.none)
                    output += "N";
                if (c.Value == InputCoverage.partial)
                {
                    output += "P";
                    trivial = false;
                }
                output += " ";
            }
            if (trivial)
            {
                output = " But trivially: system can ignore all inputs and still satisfy all requirements." + Environment.NewLine;
            }
            return output;
        }

        public string print_short_ocov()
        {
            string output = "";
            string different_value;
            foreach (var c in oCov)
            {
                different_value = "";
                if (c.Value == OutputCoverage.periodic)
                    continue;
                if (c.Key.name.Contains("_eqeq_0"))
                    output += "Output " + c.Key.name.Replace("_eqeq_0", "") + " could be ";
                else
                    if (c.Key.name.Contains("_noteq_0"))
                        output += "Output " + c.Key.name.Replace("_noteq_0", "") + " could be ";
                    else
                        if (c.Key.name.Contains("_eqeq_"))
                        {
                            output += "Output " + Regex.Replace(c.Key.name, @"_eqeq_.*", "") + " could be ";
                            different_value = Regex.Replace(c.Key.name, @".*_eqeq_", "");
                        }
                        else
                            output += "Output " + c.Key.name + " could be ";

                if (c.Value == OutputCoverage.contant)
                {
                    output += "forever ";
                    if (different_value != "")
                        output += "same as or forever different from " + different_value;
                    else
                        if (c.Key.name.Contains("_eqeq_0"))
                            output += "0";
                        else
                            if (c.Key.name.Contains("_noteq_0"))
                                output += "non-zero";
                            else
                                output += "constant";
                }
                if (c.Value == OutputCoverage.altered)
                    output += "altered just once";
                output += "." + Environment.NewLine;
            }
            if (output.Contains(" forever "))
                output = " But trivially: " + output;
            return output.TrimEnd();
        }

        public string print_long()
        {
            string output = Environment.NewLine + "The input coverage is as follows:" + Environment.NewLine;
            bool trivial = true;
            foreach (var c in iCov)
            {
                output += c.Key.name + ": ";
                if (c.Value == InputCoverage.full)
                {
                    output += "fully covered.";
                    trivial = false;
                }
                if (c.Value == InputCoverage.none)
                    output += "not covered";
                if (c.Value == InputCoverage.partial)
                {
                    output += "partially covered";
                    trivial = false;
                }
                output += Environment.NewLine;
            }
            if (trivial)
                output += "The requirements are realisable, but trivially: system can ignore all inputs and still satisfy all requirements.";
            output += Environment.NewLine + "The output coverage is as follows:" + Environment.NewLine;
            foreach (var c in oCov)
            {
                output += c.Key.name + ": ";
                switch (c.Value)
                {
                    case OutputCoverage.altered:
                        output += "must be modified once.";
                        break;
                    case OutputCoverage.contant:
                        output += "need not be modified.";
                        break;
                    case OutputCoverage.periodic:
                        output += "must be modified periodically.";
                        break;
                }
                output += Environment.NewLine;
            }
            return output;
        }
    }
    public class ByDotId : IComparer<LabelledNode>
    {
        public int Compare(LabelledNode a, LabelledNode b)
        {
            if (a.dotId == b.dotId)
                return 0;
            if (a.dotId < b.dotId)
                return -1;
            return 1;
        }
    }

    public class BySourceDotId : IComparer<LabelledEdge>
    {
        public int Compare(LabelledEdge a, LabelledEdge b)
        {
            if (a.dotFrom < b.dotFrom)
                return -1;
            if (a.dotFrom == b.dotFrom && a.dotTo < b.dotTo)
                return -1;
            if (a.dotFrom == b.dotFrom && a.dotTo == b.dotTo)
                return 0;
            return 1;
        }
    }

    public class Digraph
    {
        //**************************************************
        // Data
        //**************************************************
        List<LabelledNode> nodes;
        public List<LabelledEdge> edges;
        List<int> csrNode;
        List<Tuple<int, int>> csrEdge;

        List<int> NodeDecomposition;
        public List<HashSet<int>> EdgeDecomposition;
        List<int> index;
        List<int> lowLink;
        List<bool> onStack;

        int sccIndex = 0;
        Stack<int> S;

        public Digraph(DotGraph dg)
        {
            nodes = new List<LabelledNode>();
            edges = new List<LabelledEdge>();
            csrNode = new List<int>();
            csrEdge = new List<Tuple<int, int>>();

            //dg.print();
            load_dot_graph(dg);
            finalise();
        }

        public Digraph(TxtGraph tg)
        {
            nodes = new List<LabelledNode>();
            edges = new List<LabelledEdge>();
            csrNode = new List<int>();
            csrEdge = new List<Tuple<int, int>>();

            load_txt_graph(tg);
            finalise();
        }

        void load_txt_graph(TxtGraph tg)
        {
            foreach (var s in tg.states)
            {
                nodes.Add(new LabelledNode(s.id, s.id.ToString()));
                foreach (var e in s.successors)
                {
                    edges.Add(new LabelledEdge(s.id, e.Key, e.Value));
                }
            }
        }

        void load_dot_graph(DotGraph dg)
        {
            foreach (var i in dg.content)
            {
                if (i.t == DotGraph.Item.type.Node)
                {
                    nodes.Add(new LabelledNode(i.nodeId, i.parameters["label"]));
                }
                if (i.t == DotGraph.Item.type.Edge)
                {
                    edges.Add(new LabelledEdge(i.fromId, i.toId, i.parameters["label"]));
                }
            }
        }
        void finalise()
        {
            if (nodes.Count == 0)
                return;

            nodes.Sort(new ByDotId());
            if (edges.Count == 0)
            {
                csrNode = Enumerable.Repeat(0, nodes.Count + 1).ToList();
                return;
            }
            edges.Sort(new BySourceDotId());
            Dictionary<int, int> nodeNames = new Dictionary<int, int>();
            for (int i = 0; i < nodes.Count; i++)
                nodeNames[nodes[i].dotId] = i;

            csrNode.Add(0);
            int edgeId = 0;

            for (int nodeId = nodes[0].dotId; nodeId <= nodes.LastOrDefault().dotId; nodeId++)
            {
                while (edgeId < edges.Count && edges[edgeId].dotFrom == nodeId)
                {
                    csrEdge.Add(new Tuple<int, int>(nodeNames[nodeId],
                                                    nodeNames[edges[edgeId].dotTo]));
                    edgeId++;
                }
                csrNode.Add(edgeId);
            }
            Debug.Assert(csrNode.Count == nodes.Count + 1);
            Debug.Assert(csrEdge.Count == edges.Count);
        }
        List<int> outgoing_edges(int node)
        {
            List<int> ers = new List<int>();
            for (int i = csrNode[node]; i < csrNode[node + 1]; i++)
                ers.Add(i);
            return ers;
        }

        //**************************************************
        // Traversal functions
        //**************************************************
        void bfs_pass(Action<LabelledNode> nf, Action<LabelledEdge> ef, int startingNode)
        {
            HashSet<int> visitedNodes = new HashSet<int>();
            Queue<int> bfsQ = new Queue<int>();

            bfsQ.Enqueue(startingNode);
            while (bfsQ.Count != 0)
            {
                int n = bfsQ.Dequeue();
                nf(nodes[n]);
                foreach (var s in outgoing_edges(n))
                {
                    ef(edges[s]);
                    int toNode = csrEdge[s].Item2;
                    if (visitedNodes.Contains(toNode))
                        continue;
                    bfsQ.Enqueue(toNode);
                    visitedNodes.Add(toNode);
                }
            }
        }

        void bfs_pass_index(Action<int> nif, Action<int> eif, int startingNode)
        {
            HashSet<int> visitedNodes = new HashSet<int>();
            Queue<int> bfsQ = new Queue<int>();

            bfsQ.Enqueue(startingNode);
            while (bfsQ.Count != 0)
            {
                int n = bfsQ.Dequeue();
                nif(n);
                foreach (var s in outgoing_edges(n))
                {
                    eif(s);
                    int toNode = csrEdge[s].Item2;
                    if (visitedNodes.Contains(toNode))
                        continue;
                    bfsQ.Enqueue(toNode);
                    visitedNodes.Add(toNode);
                }
            }
        }

        public void bfs_pass_edges(Action<LabelledEdge> f)
        {
            bfs_pass((x) => { }, f, 0);
        }

        //**************************************************
        // Analysis functions
        //**************************************************
        public void scc_decomposition()
        {
            NodeDecomposition = new List<int>();
            EdgeDecomposition = new List<HashSet<int>>();
            NodeDecomposition.AddRange(Enumerable.Repeat(-1, nodes.Count));
            EdgeDecomposition.AddRange(Enumerable.Repeat(new HashSet<int>(), nodes.Count));

            index = new List<int>();
            index.AddRange(Enumerable.Repeat(-1, nodes.Count));
            lowLink = new List<int>();
            lowLink.AddRange(Enumerable.Repeat(Int32.MaxValue, nodes.Count));
            index.AddRange(Enumerable.Repeat(Int32.MaxValue, nodes.Count));
            onStack = new List<bool>();
            onStack.AddRange(Enumerable.Repeat(false, nodes.Count));

            sccIndex = 0;
            S = new Stack<int>();
            for (int n = 0; n < nodes.Count; n++)
            {
                if (index[n] == -1)
                    strong_connect(n);
            }

            for (int i = 0; i < edges.Count; i++)
                if (NodeDecomposition[csrEdge[i].Item1] == NodeDecomposition[csrEdge[i].Item2])
                    EdgeDecomposition[csrEdge[i].Item1].Add(i);
        }

        void strong_connect(int v)
        {
            index[v] = sccIndex;
            lowLink[v] = sccIndex;
            sccIndex++;
            S.Push(v);
            onStack[v] = true;

            foreach (var edgeId in outgoing_edges(v))
            {
                int w = csrEdge[edgeId].Item2;
                if (index[w] == -1)
                {
                    strong_connect(w);
                    lowLink[v] = Math.Min(lowLink[v], lowLink[w]);
                }
                else if (onStack[w])
                    lowLink[v] = Math.Min(lowLink[v], lowLink[w]);
            }

            if (lowLink[v] == index[v])
            {
                int w;
                do
                {
                    w = S.Pop();
                    onStack[w] = false;
                    NodeDecomposition[w] = v;
                } while (v != w);
            }
        }

        public HashSet<int> edge_reachability(HashSet<int> startingEdges)
        {
            HashSet<int> R = new HashSet<int>();
            foreach (var edgeId in startingEdges)
                if (!R.Contains(edgeId))
                    bfs_pass_index((n) => { }, (e) => { R.Add(e); }, csrEdge[edgeId].Item1);
            return R;
        }

        public void print()
        {
            Console.WriteLine("Nodes:");
            for (int i = 0; i < nodes.Count; i++)
            {
                Console.WriteLine(i.ToString() + ": " + nodes[i].label);
            }
            Console.WriteLine("Edges:");
            for (int i = 0; i < edges.Count; i++)
            {
                Console.WriteLine(i.ToString() + ": " + csrEdge[i].Item1.ToString() + "-" + edges[i].label + "->" + csrEdge[i].Item2.ToString());
            }
        }
    }
    public class LabelledNode
    {
        public int dotId;
        public string label;

        public LabelledNode(int id, string l)
        {
            dotId = id;
            label = l;
        }
    }

    public class LabelledEdge
    {
        public int dotFrom, dotTo;
        public string label;

        public LabelledEdge(int f, int t, string l)
        {
            dotFrom = f;
            dotTo = t;
            label = l;
        }

        public bool contains_positive(string varName)
        {
            if (contains_negative(varName))
                return false;
            else
                return label.Contains(varName);
        }

        public bool contains_negative(string varName)
        {
            string negPattern = @".*!\s*" + varName + @".*";
            Regex negRegex = new Regex(negPattern);
            Match m = negRegex.Match(label);
            return m.Success;
        }
    }

    public class TxtGraph
    {
        public class State
        {
            public int id;
            public bool initial;
            public Dictionary<int, string> successors;

            public void add_successors(string input, int from, int to)
            {
                string succ_pattern = @"\s*to state (\d+) labeled (.*)";
                Regex succ_regex = new Regex(succ_pattern);
                string temp = input.Substring(from, to - from);
                Match succ_match = succ_regex.Match(temp);
                while (succ_match.Success && succ_match.Index < to)
                {
                    successors.Add(Convert.ToInt32(succ_match.Groups[1].Captures[0].ToString()),
                                   succ_match.Groups[2].Captures[0].ToString());
                    succ_match = succ_match.NextMatch();
                }
            }

            public State(string input, int from, int to)
            {
                string label_pattern = @"\s*State (\d+).*";
                Regex label_regex = new Regex(label_pattern);
                Match label_match = label_regex.Match(input.Substring(from, to - from));
                Debug.Assert(label_match.Success);
                id = Convert.ToInt32(label_match.Groups[1].Captures[0].ToString());
                successors = new Dictionary<int, string>();
                initial = input.IndexOf("initial", from, to - from) != -1;
                from = input.IndexOf("to state", from);
                if (from != -1)
                    add_successors(input, from, to); 
            }
        }

        public List<State> states;

        public TxtGraph(string input)
        {
            states = new List<State>();
            int from = input.IndexOf("State");
            if (from == -1)
                return;
            int to = input.IndexOf("State", from + 1);
            while (to != -1)
            {
                states.Add(new State(input, from, to));
                from = to;
                to = input.IndexOf("State", from + 1);
            }
            states.Add(new State(input, from, input.Count()));
        }
    }

    public class DotGraph
    {
        public class Item
        {
            public enum type { Node, Edge, Other };
            public type t;
            String name;
            public int nodeId, fromId, toId;
            public Dictionary<String, String> parameters;

            public Item() { parameters = new Dictionary<string, string>(); }

            void parse_labels(String ls)
            {
                var lines = ToolKit.split_by(ls, ",\n");
                foreach (var l in lines)
                {
                    var new_l = l.Replace('\n', ' ');
                    string p = @"(\w*)=(.*)";
                    Regex r = new Regex(p);
                    Match m = r.Match(new_l);//TODO
                    if (m.Success)
                    {
                        parameters[m.Groups[1].Captures[0].ToString()] = m.Groups[2].Captures[0].ToString();
                        //Console.WriteLine(m.Groups[1].Captures[0].ToString() + "->" + m.Groups[2].Captures[0].ToString()  + ">>>");
                    }
                }
            }

            public int load(String input, int from)
            {
                int to = input.IndexOf(';', from);
                if (to == -1)
                    return to;
                int count = to - from + 1;
                string edgePattern = @"(?:.*\n)*.*(\d+)\s*->\s*(\d+)\s*\[((?:.*\n)*.*)\];";
                Regex edgeRegex = new Regex(edgePattern);
                string nodePattern = @"(?:.*\n)*.*(\d+)\s*\[((?:.*\n)*.*)\];";
                Regex nodeRegex = new Regex(nodePattern);
                string headPattern = @"(?:.*\n)*\s*(.+)\s*\[((?:.*\n)*.*)\];";
                Regex headRegex = new Regex(headPattern);

                Match m = edgeRegex.Match(input.Substring(from, count));
                if (m.Success)
                {
                    t = type.Edge;
                    fromId = Convert.ToInt32(m.Groups[1].Captures[0].ToString());
                    toId = Convert.ToInt32(m.Groups[2].Captures[0].ToString());
                    parse_labels(m.Groups[3].Captures[0].ToString());
                    Debug.Assert(parameters.ContainsKey("label"));
                    Debug.Assert(parameters["label"].Contains("U"));
                    return to + 1;
                }
                m = nodeRegex.Match(input.Substring(from, count));
                if (m.Success)
                {
                    t = type.Node;
                    nodeId = Convert.ToInt32(m.Groups[1].Captures[0].ToString());
                    parse_labels(m.Groups[2].Captures[0].ToString());
                    Debug.Assert(parameters.ContainsKey("label"));
                    return to + 1;
                }
                m = headRegex.Match(input.Substring(from, count));
                if (m.Success)
                {
                    t = type.Other;
                    name = m.Groups[1].Captures[0].ToString();
                    parse_labels(m.Groups[2].Captures[0].ToString());
                    return to + 1;
                }

                return -1;
            }
        }

        public List<Item> content;

        public void print()
        {
            Console.WriteLine("");
            foreach (var i in content)
            {
                if (i.t == Item.type.Edge)
                {
                    Console.WriteLine("edge");
                    continue;
                }
                if (i.t == Item.type.Node)
                {
                    Console.WriteLine("node");
                    continue;
                }
                if (i.t == Item.type.Other)
                {
                    Console.WriteLine("header");
                    continue;
                }
            }
        }

        public DotGraph(string input)
        {
            content = new List<Item>();
            int pos = input.IndexOf('{');
            while (true)
            {
                Item di = new Item();
                pos = di.load(input, pos);
                if (pos == -1)
                    break;
                content.Add(di);
            }
        }


    }
    enum VariableDomain { boolean, natural, integer, real, bitvector };

    public class Variable
    {
        public string name;
        bool input;

        public Variable(string n, bool i)
        {
            name = n;
            input = i;
        }
    }
}
