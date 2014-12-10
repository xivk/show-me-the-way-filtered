using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace AugmentedDiff.ASP
{
    class Program
    {
        static Timer _timer;

        static string _api;

        static string _osmApi;

        public static string Path;

        static object _stateLock = new object();

        public static void Initialize()
        {
            Path = @"C:\14K-SERVER-ROOT\AugmentedAugmentedDiffs\state";
            _api = "https://overpass-api.de/api/"; 
            _osmApi = "http://www.openstreetmap.org/";

            _timer = new Timer(TimerCallback, null, 100, 100);
        }

        /// <summary>
        /// Timer callback.
        /// </summary>
        /// <param name="notused"></param>
        static void TimerCallback(object notused)
        {
            _timer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

            try
            {
                // figure out what diff to process next.
                int? toGetId = null;
                var state = GetLatest(Path);
                if (state.HasValue)
                { // ok, use the state.
                    var latest = GetLatestApi(_api);
                    if (latest.HasValue)
                    { // ok, there is a latest.
                        if (latest.Value > state.Value)
                        { // ok we need to process the next one.
                            toGetId = state + 1;
                        }
                    }
                }
                else
                { // there is no state, just get the latest.
                    toGetId = GetLatestApi(_api);
                }

                // get the next diff.
                if (toGetId.HasValue)
                {
                    Console.Write("Processing changeset {0}...", toGetId.Value);

                    if (GetDiffApi(Path, _api, toGetId.Value))
                    { // downloading the diff was a success.
                        // read it and save all the changest ids.
                        HashSet<long> changesets = null;
                        var file = new FileInfo(System.IO.Path.Combine(Path, string.Format("{0}.osc", toGetId.Value)));
                        using (var fileStream = file.OpenRead())
                        {
                            var serializer = new XmlSerializer(typeof(osm));
                            changesets = GetChangesetsFromOsm(serializer.Deserialize(fileStream) as osm);
                        }

                        // get all changetset comments.
                        var comments = new Dictionary<long, string>();
                        foreach (var changesetId in changesets)
                        {
                            comments[changesetId] = GetChangesetComment(_osmApi, changesetId);
                            Thread.Sleep(100);
                        }

                        // set to a file.
                        var changesetsFile = new FileInfo(System.IO.Path.Combine(Path, string.Format("{0}.txt", toGetId.Value)));
                        using (var changesetsFileStream = new StreamWriter(changesetsFile.OpenWrite()))
                        {
                            foreach (var commentAndId in comments)
                            {
                                changesetsFileStream.WriteLine(string.Format("{0},\"{1}\"", commentAndId.Key, commentAndId.Value));
                            }
                            changesetsFileStream.Flush();
                        }

                        // ok, finished, save state.
                        SetLatest(Path, toGetId.Value);
                    }

                    Console.WriteLine("Done!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fialed: {0}", ex.ToString());
            }

            _timer.Change(100, 100);
        }

        /// <summary>
        /// Gets changesets from osm.
        /// </summary>
        /// <param name="osm"></param>
        /// <returns></returns>
        static HashSet<long> GetChangesetsFromOsm(osm osm)
        {
            var changesets = new HashSet<long>();
            if (osm != null && osm.action != null)
            {
                foreach (var action in osm.action)
                {
                    if (action.type == "create")
                    {
                        if (action.node != null)
                        {
                            foreach (var n in action.node)
                            {
                                changesets.Add(long.Parse(n.changeset));
                            }
                        }
                        if (action.way != null)
                        {
                            foreach (var w in action.way)
                            {
                                changesets.Add(long.Parse(w.changeset));
                            }
                        }
                        if (action.relation != null)
                        {
                            foreach (var r in action.relation)
                            {
                                changesets.Add(long.Parse(r.changeset));
                            }
                        }
                    }
                    else if (action.type == "modify" ||
                        action.type == "delete")
                    {
                        if (action.@new != null)
                        {
                            foreach (var a in action.@new)
                            {
                                if (a.node != null)
                                {
                                    foreach (var n in a.node)
                                    {
                                        changesets.Add(long.Parse(n.changeset));
                                    }
                                }
                                if (a.way != null)
                                {
                                    foreach (var w in a.way)
                                    {
                                        changesets.Add(long.Parse(w.changeset));
                                    }
                                }
                                if (a.relation != null)
                                {
                                    foreach (var r in a.relation)
                                    {
                                        changesets.Add(long.Parse(r.changeset));
                                    }
                                }
                            }
                        }
                        if (action.old != null)
                        {
                            foreach (var a in action.old)
                            {
                                if (a.node != null)
                                {
                                    foreach (var n in a.node)
                                    {
                                        changesets.Add(long.Parse(n.changeset));
                                    }
                                }
                                if (a.way != null)
                                {
                                    foreach (var w in a.way)
                                    {
                                        changesets.Add(long.Parse(w.changeset));
                                    }
                                }
                                if (a.relation != null)
                                {
                                    foreach (var r in a.relation)
                                    {
                                        changesets.Add(long.Parse(r.changeset));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return changesets;
        }

        /// <summary>
        /// Gets the changeset comment.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        static string GetChangesetComment(string osmApi, long id)
        {
            try
            {
                var connection = new OsmSharp.Osm.API.APIConnection(osmApi,
                    string.Empty, string.Empty);
                var changeset = connection.ChangeSetGet(id);
                var tags = changeset.Tags;
                var comment = string.Empty;
                if (tags != null && tags.TryGetValue("comment", out comment))
                {
                    if(comment == null)
                    {
                        return string.Empty;
                    }
                    return comment;
                }
                return string.Empty;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sets the latest processed diff.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="value"></param>
        static void SetLatest(string path, int value)
        {
            lock (_stateLock)
            {
                var file = new FileInfo(System.IO.Path.Combine(path, "state.txt"));
                file.Delete();
                using (var fileStream = new StreamWriter(file.OpenWrite()))
                {
                    fileStream.Write(value.ToString());
                }
            }
        }

        /// <summary>
        /// Returns the latest processed diff.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static int? GetLatest(string path)
        {
            lock (_stateLock)
            {
                var file = new FileInfo(System.IO.Path.Combine(path, "state.txt"));
                if (file.Exists)
                { // file exists, read integer.
                    using (var intTextStream = file.OpenText())
                    {
                        string intText = intTextStream.ReadToEnd();
                        int value;
                        if (int.TryParse(intText, out value))
                        { // ok, there is an integer in there.
                            return value;
                        }
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the latest changeset from the api.
        /// </summary>
        /// <param name="api"></param>
        /// <returns></returns>
        static int? GetLatestApi(string api)
        {
            try
            {
                var url = string.Format(api + "augmented_diff_status");
                var request = WebRequest.Create(new Uri(url));
                using (var response = new StreamReader(request.GetResponse().GetResponseStream()))
                {
                    var intText = response.ReadToEnd();
                    int value;
                    if (int.TryParse(intText, out value))
                    { // ok, there is an integer in there.
                        return value;
                    }
                }
            }
            catch
            {

            }
            return null;
        }

        /// <summary>
        /// Returns the given diff if present.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Stream GetDiff(string path, int id)
        {
            var file = new FileInfo(System.IO.Path.Combine(path, string.Format("{0}.osc", id)));
            if(file.Exists)
            {
                return file.OpenRead();
            }
            return null;
        }

        /// <summary>
        /// Returns the given diff if present.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="id"></param>
        /// <param name="lookBack"></param>
        /// <returns></returns>
        public static Stream GetDiff(string path, int id, int lookBack)
        {
            var osms = new List<osm>();
            var serializer = new XmlSerializer(typeof(osm));
            for(int current = id - lookBack; current <= id; current++)
            {
                var file = new FileInfo(System.IO.Path.Combine(path, string.Format("{0}.osc", id)));
                if (file.Exists)
                {
                    using (var diffStream = file.OpenRead())
                    {
                        var osm = serializer.Deserialize(diffStream) as osm;
                        osms.Add(osm);
                    }
                }
            }

            if (osms.Count > 0)
            {
                var actions = new List<osmAction>();
                foreach(var current in osms)
                {
                    actions.AddRange(current.action);
                }

                var osmComplete = new osm();
                osmComplete.generator = osms[0].generator;
                osmComplete.meta = osms[0].meta;
                osmComplete.note = osms[0].note;
                osmComplete.version = osms[0].version;
                osmComplete.action = actions.ToArray();

                var memoryStream = new MemoryStream();
                serializer.Serialize(memoryStream, osmComplete);
                memoryStream.Seek(0, SeekOrigin.Begin);
                return memoryStream;
            }
            return null;
        }

        /// <summary>
        /// Returns the given diff but filtered.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="id"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        public static Stream GetDiffFiltered(string path, int id, HashSet<string> filters)
        {
            var serializer = new XmlSerializer(typeof(osm));
            var file = new FileInfo(System.IO.Path.Combine(path, string.Format("{0}.osc", id)));
            if (file.Exists)
            {
                using (var diffStream = file.OpenRead())
                {
                    var osm = serializer.Deserialize(diffStream) as osm;
                    var changesets = GetChangesetsWith(path, id, filters);
                    var filtered = FilterChangesets(osm, changesets);

                    var memoryStream = new MemoryStream();
                    serializer.Serialize(memoryStream, filtered);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    return memoryStream;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the given diff if present.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="id"></param>
        /// <param name="lookBack"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        public static Stream GetDiffFiltered(string path, int id, int lookBack, HashSet<string> filters)
        {
            var osms = new List<osm>();
            var serializer = new XmlSerializer(typeof(osm));
            for (int current = id - lookBack; current <= id; current++)
            {
                var file = new FileInfo(System.IO.Path.Combine(path, string.Format("{0}.osc", current)));
                if (file.Exists)
                {
                    using (var diffStream = file.OpenRead())
                    {
                        var osm = serializer.Deserialize(diffStream) as osm;
                        var changesets = GetChangesetsWith(path, current, filters);
                        var filtered = FilterChangesets(osm, changesets);

                        osms.Add(filtered);
                    }
                }
            }

            if (osms.Count > 0)
            {
                var actions = new List<osmAction>();
                foreach (var current in osms)
                {
                    actions.AddRange(current.action);
                }

                var osmComplete = new osm();
                osmComplete.generator = osms[0].generator;
                osmComplete.meta = osms[0].meta;
                osmComplete.note = osms[0].note;
                osmComplete.version = osms[0].version;
                osmComplete.action = actions.ToArray();

                var memoryStream = new MemoryStream();
                serializer.Serialize(memoryStream, osmComplete);
                memoryStream.Seek(0, SeekOrigin.Begin);
                return memoryStream;
            }
            return null;
        }

        /// <summary>
        /// Returns all changeset ids in the given diffs.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="diffId"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        public static HashSet<long> GetChangesetsWith(string path, int diffId, HashSet<string> filters)
        {
            int? latest = GetLatest(path);
            var changesets = new HashSet<long>();
            if(latest.HasValue && latest.Value >= diffId)
            { // ok the diff is possibly there.
                var changesetsFile = new FileInfo(System.IO.Path.Combine(Path, string.Format("{0}.txt", diffId)));
                if(changesetsFile.Exists)
                { // the file exists, start searching it.
                    using(var changesetStream = changesetsFile.OpenText())
                    {
                        string line = changesetStream.ReadLine();
                        while(line != null)
                        {
                            bool filterOk = false;
                            foreach(var filter in filters)
                            {
                                if(line.Contains(filter))
                                {
                                    filterOk = true;
                                    break;
                                }
                            }

                            if(filterOk)
                            {
                                var splitLine = line.Split(',');
                                long changesetId = 0;
                                if(splitLine.Length > 0 && 
                                    long.TryParse(splitLine[0], out changesetId))
                                {
                                    changesets.Add(changesetId);
                                }
                            }
                            line = changesetStream.ReadLine();
                        }
                    }
                }
            }
            return changesets;
        }

        /// <summary>
        /// Filters the given osm data excluding all objects with a changeset not in the list.
        /// </summary>
        /// <param name="osm"></param>
        /// <param name="changesets"></param>
        /// <returns></returns>
        public static osm FilterChangesets(osm osm, HashSet<long> changesets)
        {
            var filterOsm = new osm();
            filterOsm.generator = osm.generator;
            filterOsm.meta = osm.meta;
            filterOsm.note = osm.note;
            filterOsm.version = osm.version;

            var actions = new List<osmAction>();
            if (osm.action != null)
            {
                foreach (var action in osm.action)
                {
                    if(action.type == "create")
                    {
                        var nodes = new List<node>();
                        if(action.node != null)
                        {
                            foreach (var n in action.node)
                            {
                                if(changesets.Contains(long.Parse(n.changeset)))
                                {
                                    nodes.Add(n);
                                }
                            }
                        }
                        var ways = new List<way>();
                        if (action.way != null)
                        {
                            foreach (var w in action.way)
                            {
                                if (changesets.Contains(long.Parse(w.changeset)))
                                {
                                    ways.Add(w);
                                }
                            }
                        }
                        var relations = new List<relation>();
                        if (action.relation != null)
                        {
                            foreach (var r in action.relation)
                            {
                                if (changesets.Contains(long.Parse(r.changeset)))
                                {
                                    relations.Add(r);
                                }
                            }
                        }

                        if(nodes.Count > 0 || ways.Count > 0 || relations.Count > 0)
                        {
                            var filteredAction = new osmAction();
                            filteredAction.type = action.type;
                            filteredAction.node = nodes.ToArray();
                            filteredAction.way = ways.ToArray();
                            filteredAction.relation = relations.ToArray();

                            actions.Add(filteredAction);
                        }
                    }
                    else
                    {
                        var actionNewFiltered = new List<osmActionNew>();
                        if(action.@new != null)
                        {
                            foreach(var actionNew in action.@new)
                            {
                                var nodes = new List<node>();
                                if (actionNew.node != null)
                                {
                                    foreach (var n in actionNew.node)
                                    {
                                        if (changesets.Contains(long.Parse(n.changeset)))
                                        {
                                            nodes.Add(n);
                                        }
                                    }
                                }
                                var ways = new List<way>();
                                if (actionNew.way != null)
                                {
                                    foreach (var w in actionNew.way)
                                    {
                                        if (changesets.Contains(long.Parse(w.changeset)))
                                        {
                                            ways.Add(w);
                                        }
                                    }
                                }
                                var relations = new List<relation>();
                                if (actionNew.relation != null)
                                {
                                    foreach (var r in actionNew.relation)
                                    {
                                        if (changesets.Contains(long.Parse(r.changeset)))
                                        {
                                            relations.Add(r);
                                        }
                                    }
                                }

                                if(nodes.Count > 0 || ways.Count > 0 || relations.Count > 0)
                                {
                                    var filteredAction = new osmActionNew();
                                    filteredAction.node = nodes.ToArray();
                                    filteredAction.way = ways.ToArray();
                                    filteredAction.relation = relations.ToArray();

                                    actionNewFiltered.Add(filteredAction);
                                }
                            }
                        }

                        var actionOldFiltered = new List<osmActionOld>();
                        if (action.old != null)
                        {
                            foreach (var actionOld in action.old)
                            {
                                var nodes = new List<node>();
                                if (actionOld.node != null)
                                {
                                    foreach (var n in actionOld.node)
                                    {
                                        if (changesets.Contains(long.Parse(n.changeset)))
                                        {
                                            nodes.Add(n);
                                        }
                                    }
                                }
                                var ways = new List<way>();
                                if (actionOld.way != null)
                                {
                                    foreach (var w in actionOld.way)
                                    {
                                        if (changesets.Contains(long.Parse(w.changeset)))
                                        {
                                            ways.Add(w);
                                        }
                                    }
                                }
                                var relations = new List<relation>();
                                if (actionOld.relation != null)
                                {
                                    foreach (var r in actionOld.relation)
                                    {
                                        if (changesets.Contains(long.Parse(r.changeset)))
                                        {
                                            relations.Add(r);
                                        }
                                    }
                                }

                                if (nodes.Count > 0 || ways.Count > 0 || relations.Count > 0)
                                {
                                    var filteredAction = new osmActionOld();
                                    filteredAction.node = nodes.ToArray();
                                    filteredAction.way = ways.ToArray();
                                    filteredAction.relation = relations.ToArray();

                                    actionOldFiltered.Add(filteredAction);
                                }
                            }
                        }

                        if(actionNewFiltered.Count > 0 || actionOldFiltered.Count > 0)
                        {
                            var filteredAction = new osmAction();
                            filteredAction.type = action.type;
                            filteredAction.@new = actionNewFiltered.ToArray();
                            filteredAction.old = actionOldFiltered.ToArray();

                            actions.Add(filteredAction);
                        }
                    }
                }
            }
            filterOsm.action = actions.ToArray();
            return filterOsm;
        }

        /// <summary>
        /// Gets the diff from the given api with the given id and writes it to the given path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="api"></param>
        /// <param name="id"></param>
        static bool GetDiffApi(string path, string api, int id)
        {
            try
            {
                var url = string.Format(api + "augmented_diff?id={0}&info=no&bbox=-180%2C-90%2C180%2C90", id);
                var request = WebRequest.Create(new Uri(url));
                using (var response = request.GetResponse().GetResponseStream())
                {
                    var file = new FileInfo(System.IO.Path.Combine(path, string.Format("{0}.osc", id)));
                    using (var fileStream = file.OpenWrite())
                    {
                        CopyStream(response, fileStream);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Copies one stream into the other.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[0x1000];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, read);
        }
    }
}