using System;
using System.Collections.Generic;

namespace TickZoom.Api
{
    public class FieldEncoderMap
    {
        public Dictionary<Type, FieldEncoder> encoders = new Dictionary<Type, FieldEncoder>();
        public FieldEncoderMap(EncodeHelper helper)
        {
            var encoder = new NumericFieldEncoder(helper);
            encoders[typeof(Byte)] = encoder;
            encoders[typeof(SByte)] = encoder;
            encoders[typeof(Int16)] = encoder;
            encoders[typeof(UInt16)] = encoder;
            encoders[typeof(Int32)] = encoder;
            encoders[typeof(UInt32)] = encoder;
            encoders[typeof(Int64)] = encoder;
            encoders[typeof(UInt64)] = encoder;
            encoders[typeof(Single)] = encoder;
            encoders[typeof(Double)] = encoder;
            encoders[typeof(TimeStamp)] = new CastingFieldEncoder(helper,typeof (long));
            encoders[typeof(Enum)] = new EnumFieldEncoder(helper);
            encoders[typeof(Boolean)] = new BooleanFieldEncoder(helper);
            encoders[typeof(string)] = new StringFieldEncoder(helper);
            encoders[typeof(SymbolInfo)] = new SymbolFieldEncoder(helper);
            encoders[typeof(Iterable<>)] = new IterableFieldEncoder(helper);
        }

        public bool TryGetValue(Type type, out FieldEncoder encoder)
        {
            return encoders.TryGetValue(type, out encoder);
        }

        public void Add(Type type, FieldEncoder encoder)
        {
            encoders.Add(type, encoder);
        }
    }
}