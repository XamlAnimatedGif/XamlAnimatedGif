// Licensed under the MIT License.
// Copyright (C) 2018 Jumar A. Macato, All Rights Reserved.

using System;
using System.Runtime.Serialization;

namespace AvaloniaGif.NewDecoder
{
    [Serializable]
    internal class GifLZWDecompressionError : Exception
    {
        public GifLZWDecompressionError()
        {
        }

        public GifLZWDecompressionError(string message) : base(message)
        {
        }

        public GifLZWDecompressionError(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected GifLZWDecompressionError(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}