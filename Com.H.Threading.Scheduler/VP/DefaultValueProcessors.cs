using Com.H.Data;
using Com.H.Net;
using Com.H.Text;
using Com.H.Text.Csv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Text.Json;
using Com.H.Text.Json;
using Com.H.Xml.Linq;
using System.IO;

namespace Com.H.Threading.Scheduler.VP
{
    public class ValueProcessorItem
    {
        public IHTaskItem Item { get; set; }
        public string Value { get; set; }
        public dynamic Data { get; set; }
        public static ValueProcessorItem Parse(IHTaskItem item)
            => new ValueProcessorItem()
            {
                Item = item,
                Value = item?.RawValue
            };

    }

    public static class DefaultValueProcessors
    {
        public static bool IsValid(
            this ValueProcessorItem valueItem,
            string contentType)
        =>
            string.IsNullOrWhiteSpace(valueItem.Value ?? valueItem?.Item?.RawValue) == false
            &&
            (valueItem?.Item?.Attributes?["content_type"]?
            .Split(new string[] { ",", "->", "=>", ">" },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)?
            .ContainsIgnoreCase(contentType) ?? false);

        public static (string BeginMarker, string EndMarker, string NullValue) GetVarMarkers(
            this ValueProcessorItem valueItem)
        {
            var beginMarker = valueItem?.Item?.Attributes?["open-marker"] ?? "{{";
            var endMarker = valueItem?.Item?.Attributes?["close-marker"] ?? "}}";
            var nullValue = valueItem?.Item?.Attributes?["null-value"] ?? "";
            return (beginMarker, endMarker, nullValue);
        }

        public static ValueProcessorItem UriProcessor(this ValueProcessorItem valueItem, CancellationToken? token = null)
        {
            if (valueItem.IsValid("uri") == false) return valueItem;
            valueItem.Value ??= valueItem.Item.RawValue;
            if (!Uri.IsWellFormedUriString(valueItem.Value, UriKind.Absolute))
                throw new FormatException(
                    $"Invalid uri format for {valueItem.Item.Name}: {valueItem.Value}");
            if ((valueItem.Value = new Uri(valueItem.Value)
                .GetContentAsync(token,
                valueItem.Item.Attributes?["uri_referer"], valueItem.Item.Attributes?["uri_user_agent"])
                .GetAwaiter().GetResult()) == null)
                throw new TimeoutException(
                    $"Uri settings retrieval timed-out for {valueItem.Item.Name}: {valueItem.Value}");
            return valueItem;
        }

        public static ValueProcessorItem DefaultVarsProcessor(this ValueProcessorItem valueItem)
        {
            if (string.IsNullOrWhiteSpace(valueItem.Value ??= valueItem?.Item?.RawValue))
                return valueItem;
            valueItem.Value = valueItem.Value.FillDate(valueItem.Item.Vars?.Now, "{now{")
                .FillDate(valueItem.Item.Vars?.Tomorrow, "{tomorrow{")
                .Replace("{dir{sys}}", Directory.GetCurrentDirectory())
                .Replace("{dir{uri}}", new Uri(Directory.GetCurrentDirectory())
                .AbsoluteUri)
                ;
            return valueItem;
        }

        public static ValueProcessorItem CustomVarsProcessor(this ValueProcessorItem valueItem)
        {
            if (string.IsNullOrWhiteSpace(valueItem.Value ??= valueItem?.Item?.RawValue)
                ||
                valueItem.Item.Vars?.Custom == null
                )
                return valueItem;
            var markers = valueItem.GetVarMarkers();
            valueItem.Value = valueItem.Value
                .Fill(valueItem.Item.Vars.Custom, 
                markers.BeginMarker, 
                markers.EndMarker,
                markers.NullValue
                );
            return valueItem;
        }

        public static ValueProcessorItem CsvDataModelProcessor(
            this ValueProcessorItem valueItem, CancellationToken? _)
        {
            if (valueItem.IsValid("csv") == false) return valueItem;
            valueItem.Value ??= valueItem.Item.RawValue;
            try
            {
                valueItem.Data = valueItem.Value.ParseCsv();
                return valueItem;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Invalid CSV Format for tag {valueItem.Item.FullName}: "
                    + ex.Message);
            }
        }

        public static ValueProcessorItem PsvDataModelProcessor(
            this ValueProcessorItem valueItem, CancellationToken? _)
        {
            if (valueItem.IsValid("psv") == false) return valueItem;
            valueItem.Value ??= valueItem.Item.RawValue;
            try
            {
                valueItem.Data = valueItem.Value.ParsePsv();
                return valueItem;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Invalid PSV Format for tag {valueItem.Item.FullName}: "
                    + ex.Message);
            }
        }

        public static ValueProcessorItem JsonDataModelProcessor(
            this ValueProcessorItem valueItem, CancellationToken? _)
        {
            if (valueItem.IsValid("json") == false) return valueItem;
            valueItem.Value ??= valueItem.Item.RawValue;
            try
            {
                valueItem.Data = valueItem.Value.ParseJson();
                return valueItem;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Invalid JSON Format for tag {valueItem.Item.FullName}: "
                    + ex.Message);
            }
        }

        public static ValueProcessorItem XmlDataModelProcessor(
            this ValueProcessorItem valueItem, CancellationToken? _)
        {
            if (valueItem.IsValid("xml") == false) return valueItem;
            valueItem.Value ??= valueItem.Item.RawValue;
            try
            {
                valueItem.Data = valueItem.Value.ParseXml(
                    bool.Parse(valueItem.Item.Attributes?["root_element"] ?? "false")
                    );
                return valueItem;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Invalid XML Format for tag {valueItem.Item.FullName}: "
                    + ex.Message);
            }
        }

    }
}
