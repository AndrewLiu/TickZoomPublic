using System;
using System.Collections.Generic;
using System.IO;

namespace TickZoom.Api
{
    public class LogicalOrderSerializer
    {
        private const int minOrderSize = 128;
        private Dictionary<int, string> tagsById = new Dictionary<int, string>();
        private Dictionary<string, int> tagsByTag = new Dictionary<string, int>();
        private int nextTagId;

        unsafe public int FromReader(LogicalOrderDefault order, MemoryStream reader)
        {
            fixed (byte* fptr = reader.GetBuffer())
            {
                
                byte* sptr = fptr + reader.Position;
                byte* ptr = sptr;
                order.binary.id = *(int*)ptr; ptr += sizeof(int);
                long binaryId = *(long*)(ptr); ptr += sizeof(long);
                order.binary.symbol = Factory.Symbol.LookupSymbol(binaryId);
                order.binary.price = *(double*)(ptr); ptr += sizeof(double);
                order.binary.position = *(int*)(ptr); ptr += sizeof(int);
                order.binary.side = (OrderSide)(*(byte*)(ptr)); ptr += sizeof(byte);
                order.binary.type = (OrderType)(*(byte*)(ptr)); ptr += sizeof(byte);
                order.binary.tradeDirection = (TradeDirection)(*(byte*)(ptr)); ptr += sizeof(byte);
                order.binary.status = (OrderStatus)(*(byte*)(ptr)); ptr += sizeof(byte);
                order.binary.strategyId = *(int*)ptr; ptr += sizeof(int);
                order.binary.strategyPosition = *(int*)(ptr); ptr += sizeof(int);
                order.binary.serialNumber = *(long*)ptr; ptr += sizeof(long);
                order.binary.utcChangeTime.Internal = *(long*)ptr; ptr += sizeof(long);
                order.binary.utcTouchTime.Internal = *(long*)ptr; ptr += sizeof(long);
                order.binary.levels = *(int*)ptr; ptr += sizeof(int);
                order.binary.levelSize = *(int*)ptr; ptr += sizeof(int);
                order.binary.levelIncrement = *(int*)ptr; ptr += sizeof(int);
                order.binary.orderFlags = (OrderFlags)(*(int*)ptr); ptr += sizeof(int);
                var tagId = *(int*)ptr; ptr += sizeof(int);
                if( tagId == 0)
                {
                    order.binary.tag = null;
                }
                else
                {
                    var length = *(int*)ptr; ptr += sizeof(int);
                    string tag;
                    if (tagsById.TryGetValue(tagId, out tag))
                    {
                        if (length != 0)
                        {
                            throw new ApplicationException("Expected length to the zero since tag is found but was " + length);
                        }
                        order.binary.tag = tag;
                    }
                    else
                    {
                        tag = new string((char*)ptr, 0, length); ptr += length * sizeof(char);
                        tagsById.Add(tagId, tag);
                        order.binary.tag = tag;
                    }
                }
                reader.Position += (int)(ptr - sptr);
            }
            return 1;
        }
        unsafe public void ToWriter(LogicalOrderDefault order, MemoryStream writer)
        {
            writer.SetLength(writer.Position + minOrderSize);
            byte[] buffer = writer.GetBuffer();
            fixed (byte* fptr = &buffer[writer.Position])
            {
                byte* ptr = fptr;
                *(int*)(ptr) = order.binary.id; ptr += sizeof(int);
                *(long*)(ptr) = order.binary.symbol.BinaryIdentifier; ptr += sizeof(long);
                *(double*)(ptr) = order.binary.price; ptr += sizeof(double);
                *(int*)(ptr) = order.binary.position; ptr += sizeof(int);
                *(byte*)(ptr) = (byte)order.binary.side; ptr += sizeof(byte);
                *(byte*)(ptr) = (byte)order.binary.type; ptr += sizeof(byte);
                *(byte*)(ptr) = (byte)order.binary.tradeDirection; ptr += sizeof(byte);
                *(byte*)(ptr) = (byte)order.binary.status; ptr += sizeof(byte);
                if (order.binary.strategy != null)
                {
                    order.binary.strategyId = order.binary.strategy.Id;
                }
                *(int*)(ptr) = order.binary.strategyId; ptr += sizeof(int);
                *(int*)(ptr) = order.binary.strategyPosition; ptr += sizeof(int);
                *(long*)(ptr) = order.binary.serialNumber; ptr += sizeof(long);
                *(long*)(ptr) = order.binary.utcChangeTime.Internal; ptr += sizeof(long);
                *(long*)(ptr) = order.binary.utcTouchTime.Internal; ptr += sizeof(long);
                *(int*)(ptr) = order.binary.levels; ptr += sizeof(int);
                *(int*)(ptr) = order.binary.levelSize; ptr += sizeof(int);
                *(int*)(ptr) = order.binary.levelIncrement; ptr += sizeof(int);
                *(int*)(ptr) = (int)order.binary.orderFlags; ptr += sizeof(int);
                int tagId;
                if( order.binary.tag == null)
                {
                    *(int*)ptr = 0; ptr += sizeof(int); // tag of 0 for null;
                }
                else
                {
                    if (tagsByTag.TryGetValue(order.binary.tag, out tagId))
                    {
                        *(int*)ptr = tagId; ptr += sizeof(int); // tag of 0 for null;
                        *(int*)ptr = 0; ptr += sizeof(int);  // Length is 0 because already defined.
                    }
                    else
                    {
                        ++nextTagId;
                        tagsByTag.Add(order.binary.tag, nextTagId);
                        *(int*)ptr = nextTagId; ptr += sizeof(int);
                        *(int*)ptr = order.binary.tag.Length; ptr += sizeof(int);
                        for (var i = 0; i < order.binary.tag.Length; i++)
                        {
                            *(char*)ptr = order.binary.tag[i]; ptr += sizeof(char);
                        }
                    }
                }
                writer.Position += ptr - fptr;
                writer.SetLength(writer.Position);
            }
        }

    }
}