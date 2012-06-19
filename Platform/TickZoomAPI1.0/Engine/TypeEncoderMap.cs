using System;
using System.Collections.Generic;

namespace TickZoom.Api
{
    public class TypeEncoderMap
    {
        public Dictionary<Type, NumericTypeEncoder> encoders = new Dictionary<Type, NumericTypeEncoder>();
        public TypeEncoderMap()
        {
            var encoder = new NumericTypeEncoder();
            encoders[typeof(byte)] = encoder;
            encoders[typeof(sbyte)] = encoder;
            encoders[typeof(Int16)] = encoder;
            encoders[typeof(UInt16)] = encoder;
            encoders[typeof(Int32)] = encoder;
            encoders[typeof (UInt32)] = encoder;
            encoders[typeof(Int64)] = encoder;
            encoders[typeof(UInt64)] = encoder;
        }

        public bool TryGetValue( Type type, out NumericTypeEncoder encoder)
        {
            return encoders.TryGetValue(type, out encoder);
        }

        public void Add( Type type, NumericTypeEncoder encoder)
        {
            encoders.Add(type,encoder);
        }
    }
}