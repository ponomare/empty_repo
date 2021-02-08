using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using GraforWpfDll.UserControls;

namespace GraforWpfDll
{
    // MathLimits keeps math limits displayed in _curvesRegion (to calculate Alpha coefficients),
    // and math limits for all curves.
    // It is used to calculate XAxis, YAxis, scrollBar, etc. 
    public class MathLimits
    {
        public MathLimits() { }

        public MathLimits(MathLimits mLimits)
        {
            this.xmin = mLimits.xmin;
            this.ymin = mLimits.ymin;
            this.xmax = mLimits.xmax;
            this.ymax = mLimits.ymax;
        }

        public MathLimits(double xmin, double ymin, double xmax, double ymax)
        {
            this.xmin = xmin;
            this.ymin = ymin;
            this.xmax = xmax;
            this.ymax = ymax;
        }
        public double xmin, ymin, xmax, ymax;
    }

    // Diapazon of pixels for GraforRegion. It is defined by size of Canvas and Region style 
    // (coefficients uf1, vf1, etc. see SetRegionsOnPage).
    public class ScreenLimits
    {
        public ScreenLimits() { }

        public ScreenLimits(double umin, double vmin, double umax, double vmax)
        {
            this.u1 = umin;
            this.v1 = vmin;
            this.u2 = umax;
            this.v2 = vmax;
        }
        public double u1, v1, u2, v2;
    }


    // GraforRegion is a part of graforPage: GraforPage includes many graforRegions. See, for example, GraforTwoRegionsPage.

    // GraforRegion consist of border and ChartRegion.

    // ChartRegion inludes areas as reion title + axis title + Axis + bt[] + curvesRegion.
    // CurvesRegion is an area for curves. CurvesRegion calculates and holds coeffisiencts to convert 
    // physical coordinates of curve's point into screen coordinates.

    public partial class GraforRegion
    {
        public int Id;
        public TRegionStyle _regionStyle = new TRegionStyle();
        //public Window _win = null;// ref to parent GraforPage to get Page size
        //public double _xmin, _xmax, _ymin, _ymax; //??
        //public double _ymin_chart, _ymax_chart, _xmin_chart, _xmax_chart;//Min and Max calculated for chart area
        //public double _alp11, _alp12, _alp21, _alp22; // Коэфф. пересчета мат.координат страницы в экранные

        public const int MAXCURVECOUNT = 2;  // for one window(page) MAXREGIONCOUNT
        public Canvas _canvas;

        //public double _u1, _v1, _u2, _v2; // u1,v1 - верхний левый угол GraforReegion = curves + axises + scrallBars
        public ScreenLimits screenLimits = new ScreenLimits();
        public bool _activeRegion = false;
        public List<TCurve> Curves;// sub for CRV;
        public TAxisX AxisX;
        public TAxisY AxisY;
        //public double _xmin_all, _ymin_all, _xmax_all, _ymax_all;//it keeps limits for all curves. Zoom = 1 for ChartRegion
        public CurvesRegion _curvesRegion = new CurvesRegion();     //curves area (x,y)= region (u,v) - scrollBars - Axises.
        public TScrollBarX _scrollBarX = new TScrollBarX();
        public TScrollBarY _scrollBarY = new TScrollBarY();

        // запретить перерисовывать region. Naprimer, esli chto-to poshlo neverno во время программной переустановки okna
        // mozhno zadat'  _repaint_region = false; i etot region ne budet pererisovyvat'sya 
        public bool _repaintRegion = true;
        public List<UIElement> _UIElements = new List<UIElement>();// keeps track of all UIs added to canvas like: canvas.Children.Add(UIEl);


        public GraforRegion()
        {
            Id = 0;
            Curves = new List<TCurve>();
            AxisX = new TAxisX();
            AxisY = new TAxisY();
            // Add event handlers for the OnScroll and OnValueChanged events.
            _scrollBarX.Scroll += new ScrollEventHandler(PositionXChanged);
            _scrollBarY.Scroll += new ScrollEventHandler(PositionYChanged);
            //_scrollBarX.ValueChanged += new RoutedPropertyChangedEventHandler(_scrollBarX.PositionChanged); 

        }
        public GraforRegion(int id, Canvas canvas)
            : this()
        {
            Id = id;
            _canvas = canvas;
        }

        public void AddCurve(TCurve crv)
        {
            Curves.Add(crv);
            InitLimitsByCurves(); //populate mathLimits and mathLimitsForAllCurves in _curvesRegion. ubrat' otsuda
        }
        // Calculates coordinates of region (u1,v1,u2,v2) based on page size.
        // Copies symbol's size from page structure to region.
        //
        //Входные:  PageWidth, PageHeight, page symw, page symh,
        //          pu1_frm,..pv2_frm parts of page, allocated for region.
        //
        // For example: 0,0,0.5,1.0 - region takes left part of page, 
        // i.e. left/upper = 0.0*PageWidth/0.0*PageHeight and right/lower = 0.5*PageWidth/1.0*PageHeight

        //Выходные:rg.u1,...v2, Symw,Symh
        public void CalculateRegionSizeAndPositionOnPage(double pageWidth, double pageHeight)
        {
            if ((int)(pageWidth) == 0 || (int)(pageHeight) == 0)
            {
                screenLimits.u1 = 0;
                screenLimits.v1 = 0;
                screenLimits.u2 = 0;
                screenLimits.v2 = 0;
            }
            else
            {
                screenLimits.u1 = (int)(pageWidth * _regionStyle.pu1_frm);
                screenLimits.v1 = (int)(pageHeight * _regionStyle.pv1_frm);
                screenLimits.u2 = (int)(pageWidth * _regionStyle.pu2_frm);
                screenLimits.v2 = (int)(pageHeight * _regionStyle.pv2_frm);
            }

            _regionStyle.symw = GraforPage.Sym.W;
            _regionStyle.symh = GraforPage.Sym.H;
        }

        // Note: 
        // - "SreenLimits for Region" should be calculated before this method
        // - "all curves" points should be defined in List<Curves> for the Region
        // - If we try to draw region in the "Zoomed mode" then Math limits should be calculated.
        // 
        //        public void DrawRegion(bool sizeChaged = true, bool limitsChanged = true)
        public void DrawRegion(PageChangedReason pageChangedReason)
        {
            //_UIElements.ForEach(x => _canvas.Children.Remove(x));
            MathToScreenMatrix _alpha;
            PageChangedReason changedReason = pageChangedReason;

            switch (pageChangedReason)
            {
                case PageChangedReason.New:
                    InitLimitsByCurves(); //populate mathLimits and mathLimitsForAllCurves in _curvesRegion
                    disp_region();// draw region: rectangle and border
                    CalculateCurvesRegionSize();
                    _alpha =_curvesRegion.CalculateAlphaCoefficients(AxisX._ascScale);
                    AxisX.DrawAxisX(_canvas, _alpha, _curvesRegion); 
                    AxisY.DrawAxisY(_canvas, _alpha, _curvesRegion);

                    SetScrollBarsSizeAndPositionOnRegion();
                    _scrollBarX.DrawScrollBar(_canvas);
                    _scrollBarY.DrawScrollBar(_canvas);
                    break;
                case PageChangedReason.SizeCanvasChanged:
                    disp_region();// draw region recanguler with background color
                    CalculateCurvesRegionSize();
                    _alpha = _curvesRegion.CalculateAlphaCoefficients(AxisX._ascScale);
                    AxisX.DrawAxisX(_canvas, _alpha, _curvesRegion);
                    AxisY.DrawAxisY(_canvas, _alpha, _curvesRegion);

                    SetScrollBarsSizeAndPositionOnRegion();
                    _scrollBarX.DrawScrollBar(_canvas);
                    _scrollBarY.DrawScrollBar(_canvas);
                    break;
                case PageChangedReason.ZoomChanged:
                    disp_region();// draw region recanguler with background color
                    CalculateCurvesRegionSize();
                    _alpha = _curvesRegion.CalculateAlphaCoefficients(AxisX._ascScale);

                    AxisX.DrawAxisX(_canvas, _alpha, _curvesRegion);
                    AxisY.DrawAxisY(_canvas, _alpha, _curvesRegion);

                    SetScrollBarsSizeAndPositionOnRegion();
                    _scrollBarX.DrawScrollBar(_canvas);
                    _scrollBarY.DrawScrollBar(_canvas);
                    break;
                case PageChangedReason.ColorRegionChanged:
                    disp_region();// draw region recanguler with background color
                   // CalculateCurvesRegionSize();
                    _alpha = _curvesRegion.CalculateAlphaCoefficients(AxisX._ascScale);

                    AxisX.DrawAxisX(_canvas, _alpha, _curvesRegion);
                    AxisY.DrawAxisY(_canvas, _alpha, _curvesRegion);

                    //SetScrollBarsSizeAndPositionOnRegion();
                    _scrollBarX.DrawScrollBar(_canvas);
                    _scrollBarY.DrawScrollBar(_canvas);
                    break;
            }

            foreach (TCurve crv in Curves)
                disp_crv(crv, _curvesRegion._alpha);
        }

        public void SetScrollBarsSizeAndPositionOnRegion()
        {
            _scrollBarX.SetScrollBarSizeAndPosition(screenLimits);
            _scrollBarY.SetScrollBarSizeAndPosition(screenLimits);
        }

        // set MaxLimits and MathLimits based on region's curves
        public void InitLimitsByCurves()
        {
            _curvesRegion.SetMathLimitsForAllCurves(Curves); 
            _curvesRegion.SetMathLimits(_curvesRegion.mathLimitsForAllCurves);
        }
        //  ScrollBar:ValueChanged event handler. 
        public void PositionXChanged(Object sender, ScrollEventArgs e)
        {
            // Display the new range in X.
            string Text = "vScrollBar Value:(OnValueChanged Event) " + e.NewValue.ToString();
            double x_del, x_ost;
            var allLimits = _curvesRegion.mathLimitsForAllCurves;
            x_del = _curvesRegion.mathLimits.xmax - _curvesRegion.mathLimits.xmin;
            x_ost = (allLimits.xmax - allLimits.xmin) - x_del;
            double xmin = allLimits.xmin + x_ost / _scrollBarX.ScrollBarDist * e.NewValue;
            double xmax = xmin + x_del;
            if (_curvesRegion.SetMathLimits(xmin, _curvesRegion.mathLimits.ymin, xmax, _curvesRegion.mathLimits.ymax) == 1) return;//if error
            _repaintRegion = true;
            DrawRegion(PageChangedReason.ZoomChanged);
            //           if (_repaintRegion == true)  DrawRegion(_canvas);
        }

        //  ScrollBar:ValueChanged event handler. 
        public void PositionYChanged(Object sender, ScrollEventArgs e)
        {
            // Display the new range in X.
            string Text = "vScrollBar Value:(OnValueChanged Event) " + e.NewValue.ToString();
            double y_del, y_ost;
            var allLimits = _curvesRegion.mathLimitsForAllCurves;
            y_del = _curvesRegion.mathLimits.ymax - _curvesRegion.mathLimits.ymin;
            y_ost = (allLimits.ymax - allLimits.ymin) - y_del;
            double ymax = allLimits.ymax - y_ost / _scrollBarY.ScrollBarDist * e.NewValue; ;
            double ymin = ymax - y_del;
            if (_curvesRegion.SetMathLimits(_curvesRegion.mathLimits.xmin, ymin, _curvesRegion.mathLimits.xmax, ymax) == 1) return;//if error
            _repaintRegion = true;
            DrawRegion(PageChangedReason.ZoomChanged);
        }

        //        public void ReDrawRegion()
        //        {
        //            DrawRegion(_canvas);
        //        }
        //        

       



        // It calculates size of region for curves = GraforRegion - scrollBars-AxisXY (for now)
        public void CalculateCurvesRegionSize()
        {
            //!!!?? Eto zavisimost' oboudnyay (loop) mezhdy Region Limits and AxisY width.

            AxisX.CalculateAxisXHeight(); // get AxisX._height
            AxisY.CalculateAxisYWidth(_curvesRegion.mathLimits); // get AxisY._pageWidth

            // Set size for "CurvesRegion" here
            _curvesRegion.CalculateScreenLimits(screenLimits, _scrollBarY.ScrollBarWidth, _scrollBarX.ScrollBarWidth, AxisY._width, AxisX._height);
            //AdjustChartSizeByAxes(AxisY._width, AxisX._height);
            // AdjustChartSizeByRegionTitle();
        }



        //public void ReSetRegionSize(double pageWidth, double pageHeight)
        //{
        //    SetRegionSize(pageWidth, pageHeight);
        //}

        //---------------------------------------------------------------
        // Calculates coordinates of region (u1,v1,u2,v2) based on page size.
        // Copies symbol's size from page structure to region.
        //
        // Входные:  PageWidth, PageHeight, page symw, page symh,
        // 
        // pu1_frm,..pv2_frm parts of page, allocated for region are used from 
        // Region structure
        //---------------------------------------------------------------
        //public void ChangeRegionSize(double width, double height, double symW, double symH)
        //{
        //    SetRegionSize(width, height, symW, symH, _tRg.pu1_frm, _tRg.pv1_frm, _tRg.pu2_frm, _tRg.pv2_frm);
        //}

        //---------------------------------------------------------------
        public int
        SetRegionStyle(double[] bt,
                       int j_dnx, //признак убывания (=-1) или возраст.(=+1) оси X  
                       int fon, // цвет фона области
                       int brd, // = 1...10 - меню с рамкой,0 - без рамки
                       int brd_clr, // цвет рамки области
                       int txth, //размер шрифта букв заголовка области
                       int txt_clr, int txt_fon, //цвет букв и фона заголовка области
                       string txt) //адрес cтроки названия области

        // zапись параметров области рисования функции в структуру RG[i]
        //double xmin,xmax;//мин. и макс. значение X в области функции
        //double ymin,ymax;//мин. и макс. значение Y в области функции
        {
            int i;

            //rg.aut = aut;
            //rg.u1 = u1; rg.v1 = v1; rg.u2 = u2; rg.v2 = v2;
            _regionStyle.naxx = 0;
            _regionStyle.naxy = 0;
            if (j_dnx < 0) j_dnx = -1;
            else j_dnx = 1;
            _regionStyle.j_dnx = j_dnx;
            _regionStyle.fon_clr = fon;
            _regionStyle.brd = brd;
            _regionStyle.brd_clr = brd_clr;
            _curvesRegion.SetBt(bt);


            //if (rg.aut == 1)
            //{
            //    rg.txtu = rg.u1 + (int)(0.5 * (rg.u2 - rg.u1 - size.Width));
            //    rg.txtv = rg.v1 + 1;
            //}
            //else
            //{
            //    //rg.txtu = txtu;
            //    //rg.txtv = txtv;
            //}
            _regionStyle.txth = txth;
            _regionStyle.txt_clr = txt_clr;
            _regionStyle.txt_fon = txt_fon;
            _regionStyle.title = txt;
            _regionStyle.win_onscreen = 0;
            _regionStyle.spec_onscreen = 0;
            _regionStyle.j_autorange = 0;
            _regionStyle.cur_clr = 2;
            _regionStyle.frm_clr = 1;

            /*    rg.u1_frm = 0;
                rg.v1_frm = 0;
                rg.u2_frm = 0;
                rg.v2_frm = 0;
             */
            return (1);
        }

        // write parameters of position of region on the page (ex: 0;0;05;05;) into regionStyle structure.
        public void SetRegionPositionParameters(double pu1_frm, double pv1_frm, double pu2_frm, double pv2_frm)
        {
            _regionStyle.pu1_frm = pu1_frm;
            _regionStyle.pv1_frm = pv1_frm;
            _regionStyle.pu2_frm = pu2_frm;
            _regionStyle.pv2_frm = pv2_frm;
        }

        //--------------------------------------------------------
        // Задание диапазона ( мин. и макс. значения по X и по Y)
        // для Region ( т.е. области рисования графиков плюс оси с названиями).
        //--------------------------------------------------------
        //public int SetLimitsForRegion(double xmin, double ymin,
        //                              double xmax, double ymax)
        //{
        //    _xmin = xmin;
        //    _xmax = xmax;
        //    _ymin = ymin;
        //    _ymax = ymax;

        //    /*
        //    _xmin_chart = _xmin;
        //    _xmax_chart = _xmax;
        //    _ymin_chart = _ymin;
        //    _ymax_chart = _ymax;
        //    */
        //    // get axes sizes, get chart size = 
        //    //eu
        //    //SetAlphaCoefficients();
        //    return 0;
        //}









        //        //--------------------------------------------------------
        //        // extend Region on width for AxisY and on height for AxisX
        //        //--------------------------------------------------------
        //        public int SetLimitsForRegionByAxes(double height, double width)
        //        {
        //
        //            //double xmin = (_chartRegion.xmin * _alpha.a11 - width) / _alpha.a11;
        //            //double ymin = (_chartRegion.ymin * _alpha.a21 - height) / _alpha.a21;
        //            double xmin = _chartRegion.xmin - width / _alpha.a11;
        //            double ymin = _chartRegion.ymin + height / _alpha.a21;
        //            SetLimitsForRegion(xmin, ymin, _xmax, _ymax);
        //
        //            return 0;
        //        }

        //--------------------------------------------------------
        // reduce Region on width for AxisY and on height for AxisX
        // to get size for chart
        //--------------------------------------------------------
        public int AdjustChartSizeByAxes(double width, double height)
        {
            _curvesRegion.screenLimits.u1 += width;
            _curvesRegion.screenLimits.v2 -= height;
            return 0;
        }

        //--------------------------------------------------------
        // reduce Region on width for AxisY and on height for AxisX
        // to get size for chart
        // TODO it doesn't include title now
        //--------------------------------------------------------
        public int AdjustChartSizeByRegionTitle()
        {
            // todo : eto verno???????????????
            _curvesRegion.screenLimits.v1 += GraforPage.GetStringSize(_regionStyle.title).Height;
            return 0;
        }


    }
}
