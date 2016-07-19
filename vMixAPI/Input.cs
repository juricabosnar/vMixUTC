﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace vMixAPI
{
    [Serializable]
    public class Input : DependencyObject, INotifyPropertyChanged
    {
        [XmlIgnore]
        public int ID
        {
            get
            {

                var inputsCfg = 0;
                foreach (var item in Elements)
                {
                    inputsCfg += item.ID % (65536 * 15);
                }

                return ((Title + Type + Duration.ToString()).GetHashCode() + inputsCfg);
            }
        }
        State _controlledState;
        [XmlIgnore]
        public State ControlledState { get { return _controlledState; } set {

                foreach (var item in Elements)
                    item.ControlledState = value;
                /*foreach (var item in Overlays)
                    item.ControlledState = value;*/
                _controlledState = value;

            } }

        [XmlAttribute("key")]
        public string Key { get; set; }
        [XmlAttribute("number")]
        public int Number { get; set; }
        [XmlAttribute("type")]
        public InputType Type { get; set; }
        [XmlAttribute("title")]
        public string Title { get; set; }

        [XmlText()]
        public string InnerTitle { get; set; }

        [XmlAttribute("state")]
        public string State { get; set; }

        [XmlAttribute("position")]
        ///Position can be not actual, call State.Update() before read
        public int Position
        {
            get { return (int)GetValue(PositionProperty); }
            set { SetValue(PositionProperty, value); RaisePropertyChanged("Position"); }
        }

        // Using a DependencyProperty as the backing store for Position.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register("Position", typeof(int), typeof(Input), new PropertyMetadata(0, InternalPropertyChanged));

        private static void InternalPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (vMixAPI.State.IsInitializing)
                return;
            switch (e.Property.Name)
            {
                case "Position":
                    ((Input)d).ControlledState.InputSetPosition(((Input)d).Number, (int)e.NewValue);
                    break;
                case "SelectedIndex":
                    ((Input)d).ControlledState.InputSelectIndex(((Input)d).Number.ToString(), (int)e.NewValue);
                    break;
                case "Loop":
                    if ((bool)e.NewValue)
                        ((Input)d).ControlledState.InputLoopOn(((Input)d).Number);
                    else
                        ((Input)d).ControlledState.InputLoopOff(((Input)d).Number);
                    break;
                case "Muted":
                    if (e.NewValue != e.OldValue)
                        ((Input)d).ControlledState.Audio(((Input)d).Number);
                    break;
            }
        }

        [XmlAttribute("duration")]
        public int Duration { get; set; }

        [XmlAttribute("selectedIndex")]
        public int SelectedIndex
        {
            get { return (int)GetValue(SelectedIndexProperty); }
            set { SetValue(SelectedIndexProperty, value); RaisePropertyChanged("SelectedIndex"); }
        }

        // Using a DependencyProperty as the backing store for SelectedIndex.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register("SelectedIndex", typeof(int), typeof(Input), new PropertyMetadata(0, InternalPropertyChanged));



        [XmlAttribute("muted")]
        public bool Muted
        {
            get { return (bool)GetValue(MutedProperty); }
            set { SetValue(MutedProperty, value); RaisePropertyChanged("Muted"); }
        }

        // Using a DependencyProperty as the backing store for Muted.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MutedProperty =
            DependencyProperty.Register("Muted", typeof(bool), typeof(Input), new PropertyMetadata(false, InternalPropertyChanged));



        [XmlAttribute("loop")]
        public bool Loop
        {
            get { return (bool)GetValue(LoopProperty); }
            set { SetValue(LoopProperty, value); RaisePropertyChanged("Loop"); }
        }

        // Using a DependencyProperty as the backing store for Loop.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LoopProperty =
            DependencyProperty.Register("Loop", typeof(bool), typeof(Input), new PropertyMetadata(false, InternalPropertyChanged));



        [XmlElement(typeof(InputText), ElementName = "text"),
            XmlElement(typeof(InputOverlay), ElementName = "overlay"),
            XmlElement(typeof(InputPosition), ElementName = "position"),
            XmlElement(typeof(InputImage), ElementName = "image")]
        public ObservableCollection<InputBase> Elements { get; set; }

        public Input()
        {
            Elements = new ObservableCollection<InputBase>();
            Elements.CollectionChanged += Elements_CollectionChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string property)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(property));
        }

        private void Elements_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            foreach (var item in e.NewItems.OfType<InputBase>())
            {
                item.InputNumber = this.Number;
                item.ControlledState = this.ControlledState;
            }
        }

    }
}
