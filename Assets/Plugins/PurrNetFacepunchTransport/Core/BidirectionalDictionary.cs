using System.Collections;
using System.Collections.Generic;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{

	public class BidirectionalDictionary<T1, T2> : IEnumerable
	{
		private readonly Dictionary<T1, T2> m_t1ToT2Dict = new();
		private readonly Dictionary<T2, T1> m_t2ToT1Dict = new();

		public IEnumerable<T1> FirstTypes => m_t1ToT2Dict.Keys;

		public IEnumerable<T2> SecondTypes => m_t2ToT1Dict.Keys;

		public int Count => m_t1ToT2Dict.Count;

		public Dictionary<T1, T2> First => m_t1ToT2Dict;

		public Dictionary<T2, T1> Second => m_t2ToT1Dict;

		public T1 this[T2 key]
		{
			get => m_t2ToT1Dict[key];
			set
			{
				Add(key, value);
			}
		}

		public T2 this[T1 key]
		{
			get => m_t1ToT2Dict[key];
			set
			{
				Add(key, value);
			}
		}

		public IEnumerator GetEnumerator() => m_t1ToT2Dict.GetEnumerator();

		public void Add(T1 key, T2 value)
		{
			if (m_t1ToT2Dict.ContainsKey(key))
			{
				Remove(key);
			}

			m_t1ToT2Dict[key] = value;
			m_t2ToT1Dict[value] = key;
		}

		public void Add(T2 key, T1 value)
		{
			if (m_t2ToT1Dict.ContainsKey(key))
			{
				Remove(key);
			}

			m_t2ToT1Dict[key] = value;
			m_t1ToT2Dict[value] = key;
		}

		public T2 Get(T1 key) => m_t1ToT2Dict[key];

		public T1 Get(T2 key) => m_t2ToT1Dict[key];

		public bool TryGetValue(T1 key, out T2 value) => m_t1ToT2Dict.TryGetValue(key, out value);

		public bool TryGetValue(T2 key, out T1 value) => m_t2ToT1Dict.TryGetValue(key, out value);

		public bool Contains(T1 key) => m_t1ToT2Dict.ContainsKey(key);

		public bool Contains(T2 key) => m_t2ToT1Dict.ContainsKey(key);

		public void Remove(T1 key)
		{
			if (Contains(key))
			{
				T2 val = m_t1ToT2Dict[key];
				m_t1ToT2Dict.Remove(key);
				m_t2ToT1Dict.Remove(val);
			}
		}

		public void Remove(T2 key)
		{
			if (Contains(key))
			{
				T1 val = m_t2ToT1Dict[key];
				m_t1ToT2Dict.Remove(val);
				m_t2ToT1Dict.Remove(key);
			}
		}
	}

}