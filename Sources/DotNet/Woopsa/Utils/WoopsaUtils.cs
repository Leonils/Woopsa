﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Woopsa
{
    static public class WoopsaUtils
    {
        public static bool IsContextWoopsaThread
        {
            get
            {
                return
                    IsContextWebServerThread ||
                    IsContextWoopsaClientSubscriptionThread ||
                    IsContextWoopsaSubscriptionServiceImplementation;
            }
        }
        internal static bool IsContextWebServerThread => WebServer.CurrentWebServer != null;
        internal static bool IsContextWoopsaClientSubscriptionThread => WoopsaClientSubscriptionChannel.CurrentChannel != null;

        internal static bool IsContextWoopsaSubscriptionServiceImplementation => WoopsaSubscriptionServiceImplementation.CurrentService != null;

        public static bool IsContextWoopsaTerminatingThread
        {
            get
            {
                if (WebServer.CurrentWebServer != null)
                    return WebServer.CurrentWebServer.Aborted;
                else if (WoopsaClientSubscriptionChannel.CurrentChannel != null)
                    return WoopsaClientSubscriptionChannel.CurrentChannel.Terminated;
                else if (WoopsaSubscriptionServiceImplementation.CurrentService != null)
                    return WoopsaSubscriptionServiceImplementation.CurrentService.Terminated;
                else
                    return false;
            }
        }

        public static string CombinePath(string basePath, string relativePath)
        {
            return basePath.TrimEnd(WoopsaConst.WoopsaPathSeparator) +
                WoopsaConst.WoopsaPathSeparator +
                RemoveInitialSeparator(relativePath);
        }

        public static string RemoveInitialSeparator(string path)
        {
            if (path.Length >= 1)
                if (path[0] == WoopsaConst.WoopsaPathSeparator)
                    return path.Substring(1);
                else
                    return path;
            else
                return path;
        }

        public static NameValueCollection ToNameValueCollection(
            this Dictionary<string, WoopsaValue> dictionary)
        {
            NameValueCollection result = new NameValueCollection();
            foreach (var item in dictionary)
                result.Add(item.Key, item.Value.AsText);
            return result;
        }

        public static TimeSpan Multiply(this TimeSpan timeSpan, int n) => TimeSpan.FromTicks(timeSpan.Ticks * n);

        /// <summary>
        /// Used to generate incremental numbers identifiers, user for example to uniquely
        /// identify in an ordered way the subscriptions.
        /// </summary>
        /// <returns></returns>
        public static ulong NextIncrementalObjectId()
        {
            lock (_instanceIndexLock)
            {
                _instanceIndex++;
                return _instanceIndex;
            }
        }

        private static ulong _instanceIndex;
        private static object _instanceIndexLock = new object();


        #region Exceptions utilities
        public static Exception RootException(this Exception e)
        {
            Exception ex = e;
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }

        public static string GetFullMessage(this Exception exception)
        {
            string eMessage = string.Empty;
            while (exception != null)
            {
                eMessage += exception.Message;
                exception = exception.InnerException;
                if (exception != null)
                    eMessage += " Inner exception: ";
            }
            return eMessage;
        }
        #endregion

        public static JsonSerializerOptions ObjectToInferredTypesConverterOptions = new JsonSerializerOptions
        {
            Converters = { new ObjectToInferredTypesConverter() }
        };
    }

    public class ObjectToInferredTypesConverter
         : JsonConverter<object>
    {
        public override object Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
                JsonTokenType.Number => reader.GetDouble(),
                JsonTokenType.String when reader.TryGetDateTime(out DateTime datetime) => datetime,
                JsonTokenType.String => reader.GetString(),
                _ => DefaultContent(ref reader)
            };
        private JsonElement DefaultContent(ref Utf8JsonReader reader)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }
        public override void Write(
            Utf8JsonWriter writer,
            object objectToWrite,
            JsonSerializerOptions options) =>
            throw new InvalidOperationException("Should not get here.");
    }
}
