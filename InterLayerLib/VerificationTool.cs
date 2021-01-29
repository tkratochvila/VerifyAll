using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace InterLayerLib
{
    /// <summary>
    /// Provides valuation of the tool's variables and allows to get the next following valuation
    /// (e.g. max steps = INF => max steps = 2000)
    /// </summary>
    public class VerificationToolVariables
    {
        public class Steps
        {
            public Steps(long number)
            {
                this.number = number;
            }
            public Steps(Steps other)
            {
                this.number = other.number;
            }
            public string human
            {
                get
                {
                   return number == -1 ? "INF" : number.ToString();
                }
                set
                {
                    number = value == "INF" ? -1 : long.Parse(value);
                }
            }
            public string str
            {
                get
                {
                    return number.ToString();
                }
                set
                {
                    number = long.Parse(value);
                }
            }
            public long number { get; set; }
        }
        public VerificationToolVariables(VerificationTool tool)
        {
            this._tool = tool;
            this._numTimeouts = 0;
            this.maxSteps = new Steps(tool.getMaxStepsAfterTimeouts(_numTimeouts));
        }
        public VerificationToolVariables(VerificationToolVariables other)
        {
            this._tool = other._tool;
            this._numTimeouts = other._numTimeouts;
            this.maxSteps = new Steps(other.maxSteps);
            this._maximumNontimeoutedSteps = other._maximumNontimeoutedSteps;
            this._minimumTimeoutedSteps = other._minimumTimeoutedSteps;
            this._infiniteMaxSteps = other._infiniteMaxSteps;
            this._propertyMacroNames = other._propertyMacroNames.ToList(); // deep copy
            this._propertyIndices = other._propertyIndices.ToList();
        }

        public string this[string index]
        {
            get
            {
                switch (index)
                {
                    case "MaxTime": return maxTime;
                    case "MaxSteps": return maxSteps.str;
                    case "CheckedRequirementsMacros": return checkedRequirementsMacros;
                    case "PropertyIndexList": return propertyIndexList;
                    default: throw new IndexOutOfRangeException("Invalid tool variable requested: " + index);
                }
            }
        }

        /// <summary>Represents the best value of MaxSteps found so far (lower bound of the current bisection interval).</summary>
        public Steps bestMaxSteps { get { return new Steps(bestMaxSteps_i); } }

        /// <summary>True if previous results suggest an improvement can be made with the current configuration</summary>
        public bool isWorthRunning
        {
            get
            {
                if (_infiniteMaxSteps == InfiniteSteps.NOT_TRIED && maxSteps.number == -1)
                    return true; // Trying infinite number of steps is beneficial if it was not tried yet
                if (_infiniteMaxSteps == InfiniteSteps.SUCCEEDED)
                    return false; // The tool was able to verify infinite number of steps
                if (_maximumNontimeoutedSteps == long.MaxValue && _infiniteMaxSteps == InfiniteSteps.TIMED_OUT)
                    return false; // The tool was able to verify arbitrary (but not infinite) number of steps
                if (_minimumTimeoutedSteps <= 1)
                    return false; // The tool timed out with MaxSteps == 0
                if (_maximumNontimeoutedSteps > (long)(_minimumTimeoutedSteps * 0.9) - 1) // - 1 to prevent infinite loop
                    return false; // The current best value is at most 10 % or 1 less than its best value ('good enough')
                return true;
            }
        }

        public VerificationTool tool { get { return _tool; } }
        public Steps maxSteps { get; private set; }
        public string maxTime { get { return tool.timeout.ToString(); } }
        public string checkedRequirementsMacros { get { return string.Join(" ", _propertyMacroNames); } }
        public string propertyIndexList { get { return string.Join(",", _propertyIndices); } }


        /// <summary>
        /// Changes the VerificationToolVariables configuration in response to a success result
        /// </summary>
        /// <created>MiD,2019-06-24</created>
        /// <changed>MiD,2019-06-24</changed>
        public void onSuccess()
        {
            this._maximumNontimeoutedSteps = Math.Max(maxSteps.number, _maximumNontimeoutedSteps);
            if (maxSteps.number == -1)
                _infiniteMaxSteps = InfiniteSteps.SUCCEEDED;
            else if (canBisectMaxSteps)
                bisectMaxSteps();
            else if (maxSteps.number < Math.Sqrt(long.MaxValue))
                maxSteps.number *= maxSteps.number; // Try to raise the maxSteps value in order to get a timeout
            else if (maxSteps.number != long.MaxValue)
                maxSteps.number = long.MaxValue; // Cannot raise by *10 so try the highest possible value if not trying already
            else if (_infiniteMaxSteps == InfiniteSteps.NOT_TRIED)
                maxSteps.number = -1;
        }

        /// <summary>
        /// Changes the VerificationToolVariables configuration in response to a timeout result
        /// </summary>
        /// <created>MiD,2019-06-24</created>
        /// <changed>MiD,2019-06-24</changed>
        public void onTimeout()
        {
            _numTimeouts++;
            if (maxSteps.number == -1)
                _infiniteMaxSteps = InfiniteSteps.TIMED_OUT;
            else
                this._minimumTimeoutedSteps = Math.Min(maxSteps.number, _minimumTimeoutedSteps);
            if (canBisectMaxSteps)
                bisectMaxSteps();
            else
                maxSteps.number = Math.Max(1, tool.getMaxStepsAfterTimeouts(_numTimeouts));
        }

        public void onPropertiesChanged(ref IEnumerable<int> propertyIndices, ref IEnumerable<string> propertyMacroNames)
        {
            this._propertyIndices = propertyIndices.ToList(); // deep copy
            this._propertyMacroNames = propertyMacroNames.ToList();
        }

        /// <summary>
        /// Sets current MaxSteps value to the best working value found so far
        /// </summary>
        /// <returns></returns>
        /// <created>MiD,2019-06-24</created>
        /// <changed>MiD,2019-06-24</changed>
        public long setBestMaxSteps()
        {
            return maxSteps.number = bestMaxSteps_i;
        }

        private enum InfiniteSteps { NOT_TRIED, SUCCEEDED, TIMED_OUT};

        private VerificationTool _tool;
        private int _numTimeouts = 0;
        private long _maximumNontimeoutedSteps = long.MinValue; // These two values are for binary search of the best number of steps for the given time
        private long _minimumTimeoutedSteps = long.MaxValue;
        private InfiniteSteps _infiniteMaxSteps = InfiniteSteps.NOT_TRIED;
        private IEnumerable<int> _propertyIndices = null;
        private IEnumerable<string> _propertyMacroNames = null;

        private bool canBisectMaxSteps { get { return _maximumNontimeoutedSteps >= 0 && _minimumTimeoutedSteps < long.MaxValue; } }
        private long bestMaxSteps_i
        {
            get
            {
                if (_infiniteMaxSteps == InfiniteSteps.SUCCEEDED)
                    return -1;
                if (_maximumNontimeoutedSteps > 0)
                    return _maximumNontimeoutedSteps;
                return 0;
            }
        }

        /// <summary>
        /// Try a new MaxSteps value in the middle of the current interval
        /// </summary>
        /// <created>MiD,2019-06-24</created>
        /// <changed>MiD,2019-06-24</changed>
        private void bisectMaxSteps()
        {
            maxSteps.number = (_minimumTimeoutedSteps + _maximumNontimeoutedSteps) / 2;
        }
    }

    public class VerificationTool
    {
        public VerificationTool()
        {
            descriptiveName = "";
            timeout = DEFAULT_TIMEOUT;
            callSchema = new ToolCallSchema();
            enabled = false;
        }

        /// <summary>Timeout to set for tool when no timeout is set</summary>
        private static readonly ulong DEFAULT_TIMEOUT = 10;
        /// <summary>Name to display to the user</summary>
        public string descriptiveName { get; set; }
        /// <summary>Name of the tool as reported by availability query from the server</summary>
        public string toolName { get; set; }
        /// <summary>Maximum time for the tool to run, in seconds</summary>
        public ulong timeout { get; set; }
        /// <summary>
        /// String determining the number of steps to try during the first iterations (e.g. "INF;2000")
        /// To find the optimum for given timeout, set to "FIND".
        /// </summary>
        public string maxSteps
        {
            get
            {
                return string.Join("; ", _maxSteps.Select(x => x<0 ? "INF" : x.ToString()));
            }
            set
            {
                if (value == "FIND") {
                    _maxSteps = new long[1];
                    _maxSteps[0] = 10; // Start the search with 10 steps
                    findMaxSteps = true;
                    return;
                }
                // Parse list of MaxSteps, values treat "INF" as -1
                if (!Regex.IsMatch(value, @"^((\s*(\d+|INF)\s*)(;\s*(\d+|INF)\s*)*)$"))
                    throw new FormatException("Invalid format for MaxSteps: " + value);
                _maxSteps = value.Split(';').Select(x => x == "INF" ? -1: long.Parse(x)).ToArray();
                findMaxSteps = false;
            }
        }
        private long[] _maxSteps = { };
        public bool findMaxSteps { get; private set;  }
        /// <summary>Model checking or requirement analysis</summary>
        public string category { get; set; }
        /// <summary>Schema for cmdline parameter translation</summary>
        private ToolCallSchema callSchema { get; set; }
        /// <summary>If the tool should be used for verification</summary>
        public bool enabled { get; set; }
        /// <summary>Is the tool sound (== does it not produce false positives)?</summary>
        public bool sound { get; set; }


        public void Load(XmlNode node)
        {
            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name)
                {
                    case "description":
                        descriptiveName = attr.Value;
                        break;
                    case "tool":
                        toolName = attr.Value;
                        break;
                    case "enabled":
                        if (attr.Value.Equals("true"))
                            enabled = true;
                        break;
                    case "sound":
                        if (attr.Value.ToLower().Equals("true"))
                            sound = true;
                        else
                            sound = false;
                        break;
                    case "category":
                        category = attr.Value;
                        break;
                    case "timeout":
                        timeout = DEFAULT_TIMEOUT;
                        ulong t;
                        if (ulong.TryParse(attr.Value, out t))
                            timeout = t;
                        break;
                    case "max_steps":
                        maxSteps = attr.Value;
                        break;
                    default:
                        // TODO: inform user?
                        break;
                }
            }
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name == "CallSchema")
                    callSchema = new ToolCallSchema(childNode);
            }
        } //Load

        /// <summary>Enables modification of the call schema using a user-friendly string. E.g. $INPUT:"main-" --use-some-option $OUTPUT</summary>
        public string CallSchema
        {
            get { return callSchema.Schema; }
            set { callSchema.Schema = value; }
        }

        /// <summary>
        /// Returns signature of the call schema, e.g. "i0" or "p0i0i1p2i3o0", to be used as CallSchema in the OSLC automation plan/request
        /// </summary>
        /// <returns></returns>
        /// <created>MiD,2019-04-10</created>
        /// <changed>MiD,2019-04-10</changed>
        public string getCallSchemaSignature()
        {
            return callSchema.getSignature();
        }

        /// <summary>
        /// Returns the list of the tool's parameters, translating variables into parameters using the variables dictionary. Throws KeyNotFoundException if tool's schema requests invalid variable type.
        /// </summary>
        /// <param name="variables">If the tool's call schema specifies a variable, it will be taken from this dictionary. If the dictionry is missing "MaxTime", it will be added based on the tool's timeout variable</param>
        /// <returns></returns>
        /// <created>MiD,2019-04-08</created>
        /// <changed>MiD,2019-04-10</changed>
        public List<string> getParameters(VerificationToolVariables variables)
        {
            return callSchema.getParameters(variables);
        }

        public long getMaxStepsAfterTimeouts(int numTimeouts)
        {
            if (_maxSteps.Length == 0)
                return -1;
            if (numTimeouts < _maxSteps.Length)
                return _maxSteps[numTimeouts];
            if (_maxSteps.Last() > 0)
                return (long)(_maxSteps.Last() / Math.Pow(10,numTimeouts - _maxSteps.Length +1)); // divide the last element by 10 for each failed timeout
            return -1;
        }

        /// <summary>
        /// Creates a list of files selected by their keys from input dictionary according to inputs of the tool's schema. Throws KeyNotFoundException if tool's schema requests invalid input type.
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        /// <created>MiD,2019-04-08</created>
        /// <changed>MiD,2019-04-08</changed>
        public List<T> chooseInputFiles<T>(IReadOnlyDictionary<string, T> files)
        {
            return callSchema.chooseInputFiles(files);
        }

        public override string ToString()
        {
            return "Name=" + descriptiveName + ", enabled=" + enabled + ", schema=" + CallSchema;
        }  
    }
}
