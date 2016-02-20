using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PrimerPipeline.Controls
{
    public sealed partial class SortableListView : ListView
    {
        #region Variables

        private string bannedSortTerm = "";
        private bool allowSorting = true, banSortTermByEquals = false, canToggleGrouping = true;

        private GridViewColumnHeader lastHeaderClicked = null;
        private ListSortDirection lastDirection = ListSortDirection.Ascending;

        private List<PropertyGroupDescription> groupDescriptions = new List<PropertyGroupDescription>();
        private List<Tuple<int, SortDescription>> sortDescriptions = new List<Tuple<int, SortDescription>>();
        
        #endregion

        public SortableListView()
            : base()
        {
            AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeaderClicked));
            KeyDown += SortableListView_KeyDown;

            VirtualizingStackPanel.SetIsVirtualizing(this, true);
            //VirtualizingPanel.SetIsVirtualizing(this, true);
            //VirtualizingPanel.SetIsVirtualizingWhenGrouping(this, true);

            //for some reason, this control seems to retain a reference to ItemsSource when it closes. So, in
            //an effort to hopefully prevent this, we'll subscribe to the loaded event, and then using this
            //we can subscribe to the closing event of the owning window, which in turn will be used to clear
            //ItemsSource:
            Loaded += SortableListView_Loaded;
        }

        private void SortableListView_Loaded(object sender, RoutedEventArgs e)
        {
            //subscribe to the window closing event, and use this to clear items source:
            Window owner = Window.GetWindow(this);

            if (owner != null)
            {
                owner.Closing += OwnerWindow_Closing;
            }
        }

        private bool CanProcess(GridViewColumnHeader clickedHeader)
        {
            if (!bannedSortTerm.Equals(""))
            {
                if (banSortTermByEquals)
                {
                    return !((Binding)clickedHeader.Column.DisplayMemberBinding).Path.Path.Equals(bannedSortTerm);
                }
                else
                {
                    return !((Binding)clickedHeader.Column.DisplayMemberBinding).Path.Path.Contains(bannedSortTerm);
                }
            }

            return true;
        }

        public void Refresh()
        {
            if (ItemsSource != null)
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(ItemsSource);
                view.Refresh();
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(this.ItemsSource != null ? this.ItemsSource : this.Items);

            dataView.SortDescriptions.Clear();
            SortDescription sD = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sD);
            dataView.Refresh();
        }

        private void ToggleGrouping()
        {
            if (canToggleGrouping && ItemsSource != null && ItemsSource is ListCollectionView)
            {
                ListCollectionView lCV = (ListCollectionView)ItemsSource;

                //is grouping currently applied:
                if (lCV.GroupDescriptions.Count > 0)
                {
                    groupDescriptions.Clear();
                    sortDescriptions.Clear();

                    //store the grouping settings so that they can later be restored:
                    for (int i = 0; i < lCV.GroupDescriptions.Count; i++)
                    {
                        groupDescriptions.Add(new PropertyGroupDescription(((PropertyGroupDescription)lCV.GroupDescriptions[i]).PropertyName));
                    }
                    
                    //and now clear grouping:
                    lCV.GroupDescriptions.Clear();

                    //if any of the current sort descriptions use the same field as the group descriptions, remove those:
                    for (int i = 0; i < lCV.SortDescriptions.Count; i++)
                    {
                        //does this have the same property as the group descriptions:
                        for (int j = 0; j < groupDescriptions.Count; j++)
                        {
                            if (groupDescriptions[j].PropertyName.Equals(lCV.SortDescriptions[i].PropertyName))
                            {
                                sortDescriptions.Add(new Tuple<int, SortDescription>(i, lCV.SortDescriptions[i]));

                                //remove the actual sort description:
                                lCV.SortDescriptions.RemoveAt(i);
                                i--;

                                break;
                            }
                        }
                    }
                }
                else
                {
                    //restore grouping:
                    for (int i = 0; i < groupDescriptions.Count; i++)
                    {
                        lCV.GroupDescriptions.Add(new PropertyGroupDescription(groupDescriptions[i].PropertyName));
                    }

                    //and clear the backup:
                    groupDescriptions.Clear();

                    //if necessary, add back in any sort descriptions that were related to grouping:
                    for (int i = 0; i < sortDescriptions.Count; i++)
                    {
                        lCV.SortDescriptions.Insert(sortDescriptions[i].Item1, sortDescriptions[i].Item2);
                    }

                    sortDescriptions.Clear();
                }
            }
        }

        #region Events

        private void GridViewColumnHeaderClicked(object sender, RoutedEventArgs e)
        {
            if (allowSorting)
            {
                GridViewColumnHeader clickedHeader = e.OriginalSource as GridViewColumnHeader;

                ListSortDirection direction;

                if (clickedHeader != null && clickedHeader.Column != null && clickedHeader.Column.DisplayMemberBinding != null && CanProcess(clickedHeader))
                {
                    if (clickedHeader.Role != GridViewColumnHeaderRole.Padding)
                    {
                        if (clickedHeader != lastHeaderClicked)
                        {
                            direction = ListSortDirection.Ascending;
                        }
                        else
                        {
                            if (lastDirection == ListSortDirection.Ascending)
                            {
                                direction = ListSortDirection.Descending;
                            }
                            else
                            {
                                direction = ListSortDirection.Ascending;
                            }
                        }

                        string sortString = "";

                        if (clickedHeader.Column.DisplayMemberBinding is MultiBinding)
                        {
                            MultiBinding mBinding = (MultiBinding)clickedHeader.Column.DisplayMemberBinding;

                            List<string> bindingPaths = new List<string>(mBinding.Bindings.Count);

                            for (int i = 0; i < mBinding.Bindings.Count; i++)
                            {
                                bindingPaths.Add(((Binding)mBinding.Bindings[i]).Path.Path);
                            }

                            sortString = bindingPaths[0];
                        }
                        else if (clickedHeader.Column.DisplayMemberBinding is Binding)
                        {
                            sortString = ((Binding)clickedHeader.Column.DisplayMemberBinding).Path.Path;
                        }

                        if (!sortString.Equals(""))
                        {
                            Sort(sortString, direction);

                            lastHeaderClicked = clickedHeader;
                            lastDirection = direction;
                        }
                    }
                }
            }
        }

        private void OwnerWindow_Closing(object sender, CancelEventArgs e)
        {
            //this is to remove references when the window closes:
            if (ItemsSource != null)
            {
                ItemsSource = null;
            }
            else if (Items != null && Items.Count > 0)
            {
                Items.Clear();
            }
        }

        private void SortableListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                //toggle grouping
                if (Keyboard.IsKeyDown(Key.G))
                {
                    ToggleGrouping();
                }
            }
        }

        #endregion

        #region Accessor methods

        public bool AllowSorting
        {
            get { return allowSorting; }
            set { allowSorting = value; }
        }

        public string BannedSortTerm
        {
            get { return bannedSortTerm; }
            set
            {
                bannedSortTerm = value;
            }
        }

        public bool BanSortTermByEquals
        {
            get { return banSortTermByEquals; }
            set
            {
                banSortTermByEquals = value;
            }
        }

        public bool CanToggleGrouping
        {
            get { return canToggleGrouping; }
            set { canToggleGrouping = value; }
        }

        #endregion
    }
}

//when closing, the control could call a RecentSettings.StoreSettings(object this) method, which in turn would save
//settings from this control, and they could be reloaded at a later time...? Would maybe need to make use of the owning window
//name as well, otherwise controls might not be unique based on just the Name property.