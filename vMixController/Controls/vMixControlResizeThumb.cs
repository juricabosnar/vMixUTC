﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;

namespace vMixController.Controls
{
    public class vMixControlResizeThumb : Thumb
    {
        public vMixControlResizeThumb()
        {
            DragDelta += new DragDeltaEventHandler(this.ResizeThumb_DragDelta);
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            vMixController.Widgets.vMixControl item = this.DataContext as vMixController.Widgets.vMixControl;

            if (item != null && !item.Locked)
            {
                double deltaHorizontal = 0;

                switch (HorizontalAlignment)
                {
                    case System.Windows.HorizontalAlignment.Right:

                        deltaHorizontal = Math.Min(-e.HorizontalChange, item.Width - 64);
                        item.Width -= deltaHorizontal;
                        item.AlignByGrid();
                        break;
                    case System.Windows.HorizontalAlignment.Left:
                        deltaHorizontal = Math.Min(e.HorizontalChange, item.Width - 64);
                        item.Width -= deltaHorizontal;
                        item.Left += deltaHorizontal;
                        item.AlignByGrid();
                        break;
                    default:
                        break;
                }

                
                
            }

            e.Handled = true;
        }
    }
}
