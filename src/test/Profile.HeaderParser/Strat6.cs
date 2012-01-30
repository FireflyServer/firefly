using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Firefly.Http;

namespace Profile.HeaderParser
{
    public class Strat6_3_WithListDictionary : Strat
    {
        public Strat6_3_WithListDictionary()
            : base(new ListDict())
        {

        }

        public override void AddRequestHeader(string name, string value)
        {
            ((ListDict)Headers).List.Add(new KeyValuePair<string, string>(name, value));
        }

        public unsafe override bool TakeMessageHeader(Baton baton, out bool endOfHeaders)
        {
            var remaining = baton.Buffer;
            endOfHeaders = false;
            if (remaining.Count < 2) return false;
            var ch0 = remaining.Array[remaining.Offset];
            var ch1 = remaining.Array[remaining.Offset + 1];
            if (ch0 == '\r' && ch1 == '\n')
            {
                endOfHeaders = true;
                baton.Skip(2);
                return true;
            }

            if (remaining.Count < 3) return false;
            var wrappedHeaders = false;
            var colonIndex = -1;
            var valueStartIndex = -1;
            var valueEndIndex = -1;
            var indexEnd = remaining.Count - 2;
            fixed (byte* pch = remaining.Array)
            {
                var scan = pch + remaining.Offset + 2;
                for (var index = 0; index != indexEnd; ++index)
                {
                    var ch2 = *scan++;
                    if (ch0 == '\r' &&
                        ch1 == '\n' &&
                        ch2 != ' ' &&
                        ch2 != '\t')
                    {
                        var name = Encoding.Default.GetString(remaining.Array, remaining.Offset, colonIndex);
                        var value = "";
                        if (valueEndIndex != -1)
                            value = Encoding.Default.GetString(remaining.Array, remaining.Offset + valueStartIndex, valueEndIndex - valueStartIndex);
                        if (wrappedHeaders)
                            value = value.Replace("\r\n", " ");
                        AddRequestHeader(name, value);
                        baton.Skip(index + 2);
                        return true;
                    }
                    if (colonIndex == -1 && ch0 == ':')
                    {
                        colonIndex = index;
                    }
                    else if (colonIndex != -1 &&
                        ch0 != ' ' &&
                        ch0 != '\t' &&
                        ch0 != '\r' &&
                        ch0 != '\n')
                    {
                        if (valueStartIndex == -1)
                            valueStartIndex = index;
                        valueEndIndex = index + 1;
                    }
                    else if (!wrappedHeaders &&
                        ch0 == '\r' &&
                        ch1 == '\n' &&
                        (ch2 == ' ' ||
                        ch2 == '\t'))
                    {
                        wrappedHeaders = true;
                    }

                    ch0 = ch1;
                    ch1 = ch2;
                }
            }
            return false;
        }
    }

    public class ListDict : IDictionary<string, string>
    {
        public readonly IList<KeyValuePair<string, string>> List = new List<KeyValuePair<string, string>>(20);

        #region Implementation of IEnumerable

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            return List.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return List.GetEnumerator();
        }

        #endregion

        #region Implementation of ICollection<KeyValuePair<string,string>>

        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
        {
            List.Add(item);
        }

        void ICollection<KeyValuePair<string, string>>.Clear()
        {
            List.Clear();
        }

        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
        {
            return List.Contains(item);
        }

        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            List.CopyTo(array,arrayIndex);
        }

        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
        {
            return List.Remove(item);
        }

        int ICollection<KeyValuePair<string, string>>.Count
        {
            get { return List.Count; }
        }

        bool ICollection<KeyValuePair<string, string>>.IsReadOnly
        {
            get { return false; }
        }

        #endregion

        #region Implementation of IDictionary<string,string>

        bool IDictionary<string, string>.ContainsKey(string key)
        {
            throw new NotImplementedException();
        }

        void IDictionary<string, string>.Add(string key, string value)
        {
            List.Add(new KeyValuePair<string, string>(key,value));
        }

        bool IDictionary<string, string>.Remove(string key)
        {
            throw new NotImplementedException();
        }

        bool IDictionary<string, string>.TryGetValue(string key, out string value)
        {
            foreach (var keyValuePair in List)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(keyValuePair.Key, key))
                {
                    value = keyValuePair.Value;
                    return true;
                }
            }
            value = default(string);
            return false;
        }

        string IDictionary<string, string>.this[string key]
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        ICollection<string> IDictionary<string, string>.Keys
        {
            get { throw new NotImplementedException(); }
        }

        ICollection<string> IDictionary<string, string>.Values
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
}
