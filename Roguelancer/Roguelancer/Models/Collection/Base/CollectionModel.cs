using Roguelancer.Interfaces;
using System.Collections.Generic;
namespace Roguelancer.Models.Collection.Base {
    /// <summary>
    /// Collection Model
    /// </summary>
    public abstract class CollectionModel<T> {
        /// <summary>
        /// Collection Model
        /// </summary>
        public List<IObjectModel> Objects { get; set; }
    }
}