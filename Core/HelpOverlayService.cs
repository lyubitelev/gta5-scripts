using System.Drawing;
using System.Windows.Forms;

namespace gta.Core
{
    internal sealed class HelpOverlayService
    {
        private const string HelpText =
            "Справка\n" +
            "F5 - открыть/закрыть эту справку\n" +
            "\n" +
            "Транспорт\n" +
            "O - стандартное меню транспорта\n" +
            "[ - онлайн транспорт  ] - избранное\n" +
            "8/2 - выбор  7/9 - страницы  5 - создать  1 - избранное\n" +
            "\n" +
            "Тюнинг\n" +
            "X - меню тюнинга в текущей машине\n" +
            "5 - открыть/применить  4/6 - изменить  0 - назад\n" +
            "Быстро: сохранить конфиг, применить сохраненный, починка, максимум\n" +
            "Разделы: модкиты, кузов, покраска, номера, колеса, свет, салон, extras\n" +
            "\n" +
            "Машина\n" +
            "Shift - реактивное нитро с огнем  H - фары всегда включены  N - починить\n" +
            "Num7/Num9 - поворотники\n" +
            "\n" +
            "Игрок и мир\n" +
            "T - замедление времени (Bullet Time / Матрица)\n" +
            "J - NoClip  L - меню оружия  Num . / Num , - меню одежды  B - полиция\n" +
            "Y - Северный Янктон  U - население Янктона\n" +
            "Num1 - шофер катает  Num3 - компаньон  Num6 - отпустить компаньонов\n" +
            "K - ударить ближайших NPC\n" +
            "\n" +
            "Esc/Back/0 - закрыть справку или вернуться назад в меню";

        private bool _isVisible;

        public bool IsVisible
        {
            get { return _isVisible; }
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
        }

        public void Draw()
        {
            if (!_isVisible)
            {
                return;
            }

            MenuPanelRenderer.Draw(HelpText, new PointF(10, 10), 0.38f);
        }

        public void Handle(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                case Keys.Back:
                case Keys.NumPad0:
                    _isVisible = false;
                    break;
            }
        }
    }
}
