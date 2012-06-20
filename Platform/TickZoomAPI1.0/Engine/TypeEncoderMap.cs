using System;
using System.Collections.Generic;

namespace TickZoom.Api
{
    public class TypeEncoderMap
    {
        public Dictionary<Type, TypeEncoder> encoders = new Dictionary<Type, TypeEncoder>();

        public bool TryGetValue( Type type, out TypeEncoder encoder)
        {
            return encoders.TryGetValue(type, out encoder);
        }

        public void Add( Type type, TypeEncoder encoder)
        {
            encoders.Add(type,encoder);
        }
    }
}