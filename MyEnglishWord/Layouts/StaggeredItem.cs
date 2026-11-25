// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MyEnglishWord.Layout;

internal class StaggeredItem
{
    public StaggeredItem(int index)
    {
        this.Index = index;
    }

    public double Top { get; internal set; }

    public double Height { get; internal set; }

    public int Index { get; }
}
