using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MyEnglishWord.Helpers;

public static class ObservableCollectionExtensions
{
    /// <summary>
    /// 原地排序，只触发 Move 事件，UI 平滑
    /// </summary>
    extension<T>(ObservableCollection<T> oc)
    {
        public void Sort(Comparison<T> comparison)
        {
            var list = new List<T>(oc);
            list.Sort(comparison);

            for (var i = 0; i < list.Count; i++)
            {
                var oldIdx = oc.IndexOf(list[i]);
                if (oldIdx != i)
                {
                    oc.Move(oldIdx, i);
                }
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                oc.Add(item);
            }
        }
    }
}
