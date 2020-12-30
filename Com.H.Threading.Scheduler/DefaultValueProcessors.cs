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

namespace Com.H.Threading.Scheduler
{
    public class ValueProcessorItem 
    {
        public IServiceItem Item { get; set; }
        public string Value { get; set; }
        public IEnumerable<dynamic> Data { get; set; }
        public static ValueProcessorItem Parse(IServiceItem item)
            => new ValueProcessorItem()
                {
                    Item = item,
                    Value = item?.RawValue
                };
        
    }

    public static class DefaultValueProcessors 
    {
        public static ValueProcessorItem UriProcessor(this ValueProcessorItem valueItem, CancellationToken? token = null)
        {
            if (!valueItem?.Item?.Attributes?["content_type"]?
                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)?
                .Contains("uri")??true) return valueItem;
            if (valueItem.Value == null) valueItem.Value = valueItem.Item.RawValue;
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

        public static ValueProcessorItem DateProcessor(this ValueProcessorItem valueItem) 
        {
            if (string.IsNullOrWhiteSpace(valueItem?.Item?.RawValue))
                return valueItem;
            if (valueItem.Value == null) valueItem.Value = valueItem.Item.RawValue;
            valueItem.Value = valueItem.Value.FillDate(valueItem.Item.Vars?.Now, "{now{")
                .FillDate(valueItem.Item.Vars?.Tomorrow, "{tomorrow{");
            return valueItem;
        }

        public static ValueProcessorItem CustomVarsProcessor(this ValueProcessorItem valueItem)
        {
            if (string.IsNullOrWhiteSpace(valueItem?.Item?.RawValue)
                ||
                valueItem.Item.Vars?.Custom == null
                ||
                typeof(IEnumerable<>).IsAssignableFrom(valueItem.Item.Vars?.Custom.GetType())
                )
                return valueItem;
            if (valueItem.Value == null) valueItem.Value = valueItem.Item.RawValue;
            valueItem.Value = valueItem.Value.Fill(valueItem.Item.Vars.Custom, "{var{", "}}");
            return valueItem;
        }

        public static ValueProcessorItem CsvDataModelProcessor(
            this ValueProcessorItem valueItem, CancellationToken? _)
        {
            if (
                string.IsNullOrWhiteSpace(valueItem?.Item?.RawValue)
                ||
                (!valueItem?.Item?.Attributes?["content_type"]?
                .Split(new string[] { ",", "->", "=>", ">" },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)?
                .Contains("csv") ?? true))
                return valueItem;
            try
            {
                valueItem.Data = valueItem.Item.RawValue.ParseCsv();
                return valueItem;
            }
            catch(Exception ex)
            {
                throw new FormatException($"Invalid CSV Format for tag {valueItem.Item.FullName}: " 
                    + ex.Message);
            }
        }

        public static ValueProcessorItem PsvDataModelProcessor(
            this ValueProcessorItem valueItem, CancellationToken? _)
        {
            if (
                string.IsNullOrWhiteSpace(valueItem?.Item?.RawValue)
                ||
                (!valueItem?.Item?.Attributes?["content_type"]?
                .Split(new string[] { ",", "->", "=>", ">" }, 
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )?
                .Contains("psv") ?? true)
                )
                return valueItem;
            try
            {
                valueItem.Data = valueItem.Item.RawValue.ParsePsv();
                return valueItem;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Invalid PSV Format for tag {valueItem.Item.FullName}: "
                    + ex.Message);
            }
        }

    }
}
