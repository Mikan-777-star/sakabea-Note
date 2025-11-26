using System;
using System.ComponentModel;
using System.Windows.Forms;

// 1. カスタムEventArgs（どのタブか + キャンセル可能フラグ）
public class TabClosingEventArgs : CancelEventArgs
{
    public TabPage TabPage { get; }

    // コンストラクタで、閉じようとしているタブページを渡すわ
    public TabClosingEventArgs(TabPage tabPage) : base()
    {
        this.TabPage = tabPage;
    }
}

// 2. カスタムデリゲート（イベントの型）
public delegate void TabClosingEventHandler(object? sender, TabClosingEventArgs e);