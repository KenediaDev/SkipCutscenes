using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Kenedia.Modules.SkipCutscenes
{
    public enum _Controls
    {
    }
    public enum _Icons
    {
        Bug,
        ModuleIcon,
        ModuleIcon_Hovered,
        ModuleIcon_HoveredWhite,
        ModuleIcon_Active,
        ModuleIcon_Active_Hovered,
        ModuleIcon_Active_HoveredWhite,
    }
    public enum _Emblems
    {
        QuestionMark,
    }
    public enum _Backgrounds
    {
        MainWindow,
        Tooltip,
    }

    public class TextureManager : IDisposable
    {
        public List<Texture2D> _Backgrounds = new List<Texture2D>();
        public List<Texture2D> _Icons = new List<Texture2D>();
        public List<Texture2D> _Emblems = new List<Texture2D>();
        public List<Texture2D> _Controls = new List<Texture2D>();

        private bool disposed = false;
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;

                _Backgrounds?.DisposeAll();
                _Icons?.DisposeAll();
                _Emblems?.DisposeAll();
                _Controls?.DisposeAll();
            }
        }

        public TextureManager()
        {
            var ContentsManager = SkipCutscenes.ModuleInstance.ContentsManager;

            var values = Enum.GetValues(typeof(_Backgrounds));
            if (values.Length > 0)
            {
                _Backgrounds = new List<Texture2D>(new Texture2D[values.Cast<int>().Max() + 1]);
                foreach (_Backgrounds num in values)
                {
                    var texture = ContentsManager.GetTexture(@"textures\backgrounds\" + (int)num + ".png");
                    if (texture != null) _Backgrounds.Insert((int)num, texture);
                }
            }

            values = Enum.GetValues(typeof(_Icons));
            if (values.Length > 0)
            {
                _Icons = new List<Texture2D>(new Texture2D[values.Cast<int>().Max() + 1]);
                foreach (_Icons num in values)
                {
                    var texture = ContentsManager.GetTexture(@"textures\icons\" + (int)num + ".png");
                    if (texture != null) _Icons.Insert((int)num, texture);
                }
            }

            values = Enum.GetValues(typeof(_Controls));
            if (values.Length > 0)
            {
                _Controls = new List<Texture2D>(new Texture2D[values.Cast<int>().Max() + 1]);
                foreach (_Controls num in values)
                {
                    var texture = ContentsManager.GetTexture(@"textures\controls\" + (int)num + ".png");
                    if (texture != null) _Controls.Insert((int)num, texture);
                }
            }
        }

        public Texture2D getBackground(_Backgrounds background)
        {
            var index = (int)background;

            if (index < _Backgrounds.Count && _Backgrounds[index] != null) return _Backgrounds[index];
            return _Icons[0];
        }

        public Texture2D getIcon(_Icons icon)
        {
            var index = (int)icon;

            if (index < _Icons.Count && _Icons[index] != null) return _Icons[index];
            return _Icons[0];
        }

        public Texture2D getEmblem(_Emblems emblem)
        {
            var index = (int)emblem;
            if (index < _Emblems.Count && _Emblems[index] != null) return _Emblems[index];
            return _Icons[0];
        }

        public Texture2D getControlTexture(_Controls control)
        {
            var index = (int)control;
            if (index < _Controls.Count && _Controls[index] != null) return _Controls[index];
            return _Icons[0];
        }
    }
}
