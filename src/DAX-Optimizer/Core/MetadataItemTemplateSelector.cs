using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace DAX_Optimizer.Core
{
    public class MetadataItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TableTemplate { get; set; }
        public DataTemplate ColumnTemplate { get; set; }
        public DataTemplate MeasureTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is MetadataItem metadataItem)
            {
                switch (metadataItem.Type)
                {
                    case MetadataItemType.Table:
                        return TableTemplate;
                    case MetadataItemType.Column:
                        return ColumnTemplate;
                    case MetadataItemType.Measure:
                        return MeasureTemplate;
                    default:
                        return base.SelectTemplate(item, container);
                }
            }
            return base.SelectTemplate(item, container);
        }
    }
}
