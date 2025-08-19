using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DAX_Optimizer.Core
{
    public class MetadataItem
    {
        public string Name { get; set; }
        public MetadataItemType Type { get; set; }
        public string Icon { get; set; }
        public MetadataItem Parent { get; set; }
        public string DataType { get; set; }
        public string Format { get; set; }
        public string SortByColumn { get; set; }
        public string Description { get; set; }
        public string Expression { get; set; }

        public string DisplayName => $"{Icon} {Name}";
        public int Level => Parent == null ? 0 : 1;
        public Thickness Margin => new Thickness(Level * 20, 0, 0, 0);
    }

}
