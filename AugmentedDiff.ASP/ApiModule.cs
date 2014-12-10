namespace AugmentedDiff.ASP
{
    using Nancy;
    using Nancy.ModelBinding;
    using System.Collections.Generic;

    /// <summary>
    /// The main api module.
    /// </summary>
    public class ApiModule : NancyModule
    {
        /// <summary>
        /// Creates a new api module.
        /// </summary>
        public ApiModule()
        {
            Get[""] = _ =>
            {
                return Negotiate.WithStatusCode(HttpStatusCode.OK).WithView("index");
            };
            Get["/augmented_diff"] = _ =>
            {
                var query = this.Bind<Query>();
                int id;
                if (!string.IsNullOrWhiteSpace(query.id) &&
                    int.TryParse(query.id, out id))
                { // there is at least an API.
                    int lookBack;
                    if (!string.IsNullOrWhiteSpace(query.lookBack) &&
                        int.TryParse(query.lookBack, out lookBack))
                    {
                        if (string.IsNullOrWhiteSpace(query.tags))
                        { // no extra tags, just return 
                            var diffStream = Program.GetDiff(Program.Path, id, lookBack);
                            if (diffStream != null)
                            {
                                return Response.FromStream(diffStream, "application/xml");
                            }
                        }
                        else
                        {
                            var filters = new HashSet<string>();
                            foreach (var filter in query.tags.Split(','))
                            {
                                filters.Add(filter);
                            }
                            var diffStream = Program.GetDiffFiltered(Program.Path, id, lookBack, filters);
                            if (diffStream != null)
                            {
                                return Response.FromStream(diffStream, "application/xml");
                            }
                        }
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(query.tags))
                        { // no extra tags, just return 
                            var diffStream = Program.GetDiff(Program.Path, id);
                            if (diffStream != null)
                            {
                                return Response.FromStream(diffStream, "application/xml");
                            }
                        }
                        else
                        {
                            var filters = new HashSet<string>();
                            foreach (var filter in query.tags.Split(','))
                            {
                                filters.Add(filter);
                            }
                            var diffStream = Program.GetDiffFiltered(Program.Path, id, filters);
                            if (diffStream != null)
                            {
                                return Response.FromStream(diffStream, "application/xml");
                            }
                        }
                    }
                }
                return Negotiate.WithStatusCode(HttpStatusCode.NotFound);
            };
            Get["/augmented_diff_status"] = _ =>
            {
                int? latest = Program.GetLatest(Program.Path);
                if (latest.HasValue)
                {
                    return latest.ToString();
                }
                return (-1).ToString();
            };
        }
    }
}