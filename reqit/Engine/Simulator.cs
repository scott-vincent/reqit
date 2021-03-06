﻿using reqit.Models;
using reqit.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace reqit.Engine
{
    public class Simulator : ISimulator
    {
        private Random random = new Random();

        private readonly IResolver resolver;
        private readonly IFormatter formatter;
        private readonly IJsonParser jsonParser;

        public Simulator(IResolver resolver, IFormatter formatter, IJsonParser jsonParser)
        {
            this.resolver = resolver;
            this.formatter = formatter;
            this.jsonParser = jsonParser;
        }

        public string GetRequest(Api.Methods method, string path, Dictionary<string, string> query)
        {
            // The cache contains resolved values only and stores values
            // for path variables, query variables and all the request attributes.

            // Get matched api and create cache containing path variables
            Api api = this.resolver.MatchRoute(method, path, out var cache);
            if (api == null)
            {
                throw new KeyNotFoundException();
            }

            // Add query pairs to cache
            foreach (KeyValuePair<string, string> pair in query)
            {
                cache.AddResolved("query." + pair.Key, pair.Value);
            }

            if (api.Request == null)
            {
                throw new ArgumentException($"The called API does not have a request defined in the YAML file");
            }

            // Get request
            Entity request;
            try
            {
                request = this.resolver.FindEntity(api.Request.EntityDef);
                return this.formatter.EntityToJson(request, cache, api.Request.Mods);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Failed to resolve request entity: {e.Message}");
            }
        }

        public string Call(Api.Methods method, string path, Dictionary<string, string> query, string request)
        {
            // The cache contains resolved values only and stores values
            // for path variables, query variables and all the request attributes.

            // Get matched api and create cache containing path variables
            Api api = this.resolver.MatchRoute(method, path, out var cache);
            if (api == null)
            {
                throw new KeyNotFoundException();
            }

            // Add query pairs to cache
            foreach (KeyValuePair<string, string> pair in query)
            {
                cache.AddResolved("query." + pair.Key, pair.Value);
            }

            // Parse request and add to cache
            if (request == null)
            {
                // Fail if request is expected
                if (api.Request != null)
                {
                    throw new ArgumentException("Request body was expected but not supplied");
                }
            }
            else
            {
                // Fail if request is not expected
                if (api.Request == null)
                {
                    throw new ArgumentException("Request body was supplied but not expected. " +
                            "The called API does not have a request defined in the YAML file");
                }

                Entity requestEntity;
                try
                {
                    requestEntity = this.jsonParser.LoadEntity(api.Request.EntityName, request);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Failed to parse request JSON: {e.Message}");
                }

                RequestToCache("request", requestEntity, cache);

                // If any modded request attributes are missing from the request,
                // add them to the cache.
                foreach (var entry in api.Request.Mods)
                {
                    if (entry.Value != null)
                    {
                        string attrib = "request." + entry.Key;
                        if (!cache.HasValue(attrib))
                        {
                            if (entry.Value[0] == '~')
                            {
                                // Mod is a reference
                                try
                                {
                                    string value = cache.GetValue(entry.Value.Substring(1)).Value;
                                    cache.AddResolved(attrib, value);
                                }
                                catch (Exception)
                                {
                                    // Do nothing
                                }
                            }
                            else
                            {
                                cache.AddResolved(attrib, entry.Value);
                            }
                        }
                    }
                }
            }

            Entity response = null;
            if (api.Response != null)
            {
                // Get response entity
                try
                {
                    response = this.resolver.FindEntity(api.Response.EntityDef);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Response entity {e.Message}");
                }

                if (response == null)
                {
                    return null;
                }
            }

            string json = null;
            if (api.Persist == null)
            {
                if (response != null)
                {
                    // Generate response
                    try
                    {
                        addResponseMods(cache, api.Response.Mods);
                        json = this.formatter.EntityToJson(response, cache, api.Response.Mods);
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException($"Failed to resolve response entity: {e.Message}");
                    }
                }
            }
            else if (api.Method == Api.Methods.GET)
            {
                if (response != null)
                {
                    // Determine if response is an array or single object
                    while (response.Type == Entity.Types.REF)
                    {
                        Entity refEntity;

                        try
                        {
                            refEntity = this.resolver.FindEntity(response.Value);
                        }
                        catch (Exception e)
                        {
                            throw new ArgumentException($"Entity '{response.Name}' has bad reference '{response.Value}': {e.Message}");
                        }

                        response = refEntity;
                    }

                    if (response.Type == Entity.Types.REPEAT)
                    {
                        // Get array response from persisted files
                        var sb = new StringBuilder();
                        foreach (var filename in Directory.GetFiles(api.Persist.Folder, api.Persist.WildPattern).OrderBy(f => f))
                        {
                            if (sb.Length == 0)
                            {
                                sb.Append("[");
                            }
                            else
                            {
                                sb.Append(", ");
                            }

                            sb.Append(LoadResponse(filename, cache, api, false));
                        }

                        if (sb.Length == 0)
                        {
                            sb.Append("[]");
                        }
                        else
                        {
                            sb.Append("]");
                        }

                        json = sb.ToString();
                    }
                    else
                    {
                        // Get object response from persisted file
                        string filename;
                        try
                        {
                            filename = api.Persist.InsertVars(cache, api.Request, api.Response);
                        }
                        catch (Exception e)
                        {
                            throw new ArgumentException(e.Message);
                        }

                        bool forceResolve = false;
                        if (filename.Contains('*'))
                        {
                            // Choose file at random
                            var filenames = Directory.GetFiles(api.Persist.Folder, Path.GetFileName(filename));
                            if (filenames.Length == 0)
                            {
                                throw new KeyNotFoundException();
                            }

                            int chosen = random.Next(filenames.Length);
                            filename = filenames[chosen];

                            // Need to force resolve so id gets replaced
                            // with requested one (by response mods).
                            forceResolve = true;
                        }
                        else
                        {
                            // Load specific file
                            if (!File.Exists(filename))
                            {
                                throw new KeyNotFoundException();
                            }
                        }

                        json = LoadResponse(filename, cache, api, forceResolve);
                    }
                }
            }
            else
            {
                if (response != null)
                {
                    // Generate response
                    try
                    {
                        json = this.formatter.EntityToJson(response, cache, api.Response.Mods);
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException($"Failed to resolve response entity: {e.Message}");
                    }
                }

                string filename;
                try
                {
                    filename = api.Persist.InsertVars(cache, api.Request, api.Response);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(e.Message);
                }

                if (api.Method == Api.Methods.DELETE)
                {
                    // Delete persisted file
                    File.Delete(filename);
                }
                else
                {
                    // Save persisted file
                    File.WriteAllText(filename, json);
                }
            }

            return json;
        }

        /// <summary>
        /// Adds the request values to the cache by walking the entity hierarchy
        /// and updating the cache with all the retrieved values.
        /// </summary>
        private void RequestToCache(string parentName, Entity entity, Cache cache)
        {
            string fullName;
            if (parentName != null)
            {
                fullName = parentName + "." + entity.Name;
            }
            else
            {
                fullName = entity.Name;
            }

            if (entity.Type == Entity.Types.PARENT | entity.Type == Entity.Types.ARRAY)
            {
                if (entity.Name[0] == '~')
                {
                    // Need to store array parent so that repeat count can be determined
                    cache.AddResolved(fullName, "repeat");
                }

                foreach (var entityName in entity.ChildOrder)
                {
                    Entity child = entity.ChildEntities[entityName];
                    RequestToCache(fullName, child, cache);
                }
            }
            else
            {
                cache.AddResolved(fullName, entity.Value);
            }
        }

        /// <summary>
        /// Load JSON from persisted file. If it contains funcs
        /// these need to be resolved so convert JSON to entity,
        /// then convert entity back to JSON which will resolve
        /// all funcs.
        /// </summary>
        private string LoadResponse(string filename, Cache cache, Api api, bool forceResolve)
        {
            string json = File.ReadAllText(filename);

            // Only do double conversion if we have to
            if (forceResolve || json.Contains("func."))
            {
                Entity response;
                try
                {
                    response = this.jsonParser.LoadEntity(api.Response.EntityName, json);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Failed to parse JSON in persist file '{filename}': {e.Message}");
                }

                try
                {
                    return this.formatter.EntityToJson(response, cache, api.Response.Mods);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Failed to resolve persist file '{filename}': {e.Message}");
                }
            }

            return json;
        }

        /// <summary>
        /// Resolve all mods and add to cache (ignore special cases)
        /// </summary>
        private void addResponseMods(Cache cache, Dictionary<string, string> mods)
        {
            foreach (var mod in mods)
            {
                ResolvedValue resolving;

                if (!mod.Key.Equals("*") && mod.Value != null)
                {
                    if (mod.Value[0] == '~')
                    {
                        // Try to retrieve modded value from cache
                        string findName = mod.Value.Substring(1);

                        var refEntity = this.resolver.FindEntity(findName);
                        resolving = new ResolvedValue(findName, refEntity.Type, refEntity.Value);
                        this.resolver.Resolve(resolving, cache, this.formatter);

                        // Also add to cache using mod key name
                        cache.AddResolved(mod.Key, resolving.Value);
                    }
                    else
                    {
                        // Mod value is a literal
                        string value;
                        if (mod.Value[0] == '"' && mod.Value[mod.Value.Length - 1] == '"')
                        {
                            value = mod.Value.Substring(1, mod.Value.Length - 2);
                        }
                        else
                        {
                            value = mod.Value;

                            // Still needs resolving if it contains funcs
                            //if (value.Contains("func."))
                            //{
                            //    resolving = new ResolvedValue(mod.Key, Entity.Types.STR, value);
                            //    this.resolver.Resolve(resolving, cache, this.formatter);
                            //    return;
                            //}
                        }

                        cache.AddResolved(mod.Key, value);
                    }
                }
            }
        }
    }
}
