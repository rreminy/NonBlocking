﻿// Copyright (c) Vladimir Sadov. All rights reserved.
//
// This file is distributed under the MIT License. See LICENSE.md for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NonBlocking
{
    internal abstract class DictionaryImpl<TKey, TValue>
        : DictionaryImpl
    {
        internal readonly bool valueIsValueType = typeof(TValue).IsValueType;
        internal IEqualityComparer<TKey> _keyComparer;

        internal DictionaryImpl() { }

        internal abstract void Clear();
        internal abstract void Clear(int capacity);
        internal abstract int Count { get; }
        internal abstract int EstimatedCount { get; }
        internal abstract int Capacity { get; }

        internal abstract object TryGetValue(TKey key);
        internal abstract bool PutIfMatch(TKey key, TValue newVal, ref TValue oldValue, ValueMatch match);
        internal abstract bool RemoveIfMatch(TKey key, ref TValue oldValue, ValueMatch match);
        internal abstract TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory);

        internal abstract Snapshot GetSnapshot();

        internal abstract class Snapshot
        {
            protected int _idx;
            protected TKey _curKey;
            protected TValue _curValue;

            public abstract int Count { get; }
            public abstract bool MoveNext();
            public abstract void Reset();

            internal DictionaryEntry Entry
            {
                get
                {
                    return new DictionaryEntry(_curKey, _curValue);
                }
            }

            internal KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    return new KeyValuePair<TKey, TValue>(this._curKey, _curValue);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected TValue FromObjectValue(object obj)
        {
            // regular value type
            if (default(TValue) != null)
            {
                return Unsafe.As<Boxed<TValue>>(obj).Value;
            }

            // null
            if (obj == NULLVALUE)
            {
                return default(TValue);
            }

            // ref type
            if (!valueIsValueType)
            {
                return Unsafe.As<object, TValue>(ref obj);
            }

            // nullable
            return (TValue)obj;
        }
    }
}
