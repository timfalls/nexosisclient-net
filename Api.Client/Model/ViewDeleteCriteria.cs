﻿using System.Collections.Generic;
using Nexosis.Api.Client.Utility;

namespace Nexosis.Api.Client.Model
{
    public class ViewDeleteCriteria
    {
        public ViewDeleteCriteria(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// The name of the View to delete
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Options for cascading the delete.<br/>
        /// When cascade=sessions, also deletes sessions associated with the view
        /// </summary>
        public ViewCascadeOptions? Cascade { get; set; }

        internal List<KeyValuePair<string, string>> ToParameters()
        {
            var builder = new ParameterBuilder();
            
            if (Cascade.HasValue && (Cascade & ViewCascadeOptions.CascadeSessions) != 0)
            {
                builder.Add("cascade", "sessions");
            }
            return builder.GetParameters();
        }
    }
}