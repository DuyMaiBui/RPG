using System.Collections.Generic;

namespace AuraEngine.Core
{
    public sealed class AuraBodyStateComparer : IComparer<AuraBodyState>
    {
        public static readonly AuraBodyStateComparer Instance = new AuraBodyStateComparer();

        int IComparer<AuraBodyState>.Compare(AuraBodyState x, AuraBodyState y)
        {
            var entity = x.Entity.Index.CompareTo(y.Entity.Index);
            if (entity != 0)
                return entity;

            var generation = x.Entity.Generation.CompareTo(y.Entity.Generation);
            if (generation != 0)
                return generation;

            var body = x.Body.Index.CompareTo(y.Body.Index);
            if (body != 0)
                return body;

            return x.Body.Generation.CompareTo(y.Body.Generation);
        }
    }
}
