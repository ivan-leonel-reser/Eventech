using System;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Seccion Maestros del menu lateral: el catalogo de servicios y productos y su
    // bitacora de cambios (T06.B), en dos pestanas. Cada pestana se agrega solo si el
    // perfil tiene su permiso (SERVICIOS_GESTION para el catalogo; ver o activar
    // versiones para la bitacora). Las dos muestran la misma tabla desde dos lados:
    // al volver a una pestana se vuelve a leer, porque en la otra se pudo dar de alta,
    // modificar o activar una version. Antes de dejar el catalogo con cambios sin
    // guardar se pregunta (IVistaConCambios, como al cambiar de seccion).
    public class ucMaestros_704ILR : UserControl, IObservadorIdioma_704ILR, IAyudaContextual_704ILR, IVistaConCambios_704ILR
    {
        // Pedido de cerrar la seccion (boton Salir de la bitacora): lo atiende frmMain.
        public event EventHandler SalirSolicitado_704ILR;

        private readonly TabControl _tabs_704ILR;
        private readonly TabPage _tabServicios_704ILR;     // null sin SERVICIOS_GESTION
        private readonly TabPage _tabCambios_704ILR;       // null sin permiso sobre la bitacora
        private readonly ucServicios_704ILR _servicios_704ILR;
        private readonly ucBitacoraCambios_704ILR _cambios_704ILR;
        private bool _preguntando_704ILR;

        public ucMaestros_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            _tabs_704ILR = new TabControl { Dock = DockStyle.Fill, Font = Theme_704ILR.FontBody_704ILR };

            if (Permisos_704ILR.Tiene_704ILR("SERVICIOS_GESTION"))
            {
                _tabServicios_704ILR = NuevaPestana_704ILR();
                _servicios_704ILR = new ucServicios_704ILR { Dock = DockStyle.Fill };
                _tabServicios_704ILR.Controls.Add(_servicios_704ILR);
                _tabs_704ILR.TabPages.Add(_tabServicios_704ILR);
            }
            if (Permisos_704ILR.TieneAlguno_704ILR(BLL_Servicio_C_704ILR.PermisoVer_704ILR, BLL_Servicio_C_704ILR.PermisoActivar_704ILR))
            {
                _tabCambios_704ILR = NuevaPestana_704ILR();
                _cambios_704ILR = new ucBitacoraCambios_704ILR { Dock = DockStyle.Fill };
                _cambios_704ILR.SalirSolicitado_704ILR += (s_704ILR, e_704ILR) => SalirSolicitado_704ILR?.Invoke(this, EventArgs.Empty);
                _tabCambios_704ILR.Controls.Add(_cambios_704ILR);
                _tabs_704ILR.TabPages.Add(_tabCambios_704ILR);
            }
            _tabs_704ILR.Deselecting += Tabs_Deselecting_704ILR;
            _tabs_704ILR.Selected += Tabs_Selected_704ILR;
            Controls.Add(_tabs_704ILR);

            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        private static TabPage NuevaPestana_704ILR() => new TabPage
        {
            BackColor = Theme_704ILR.BgContent_704ILR,
            Padding = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0),
            UseVisualStyleBackColor = true
        };

        // F1: el tema de la pestana a la vista.
        public string TemaAyuda_704ILR =>
            _tabs_704ILR.SelectedTab != null && _tabs_704ILR.SelectedTab == _tabCambios_704ILR
                ? Ayuda_704ILR.TemaBitacoraCambios_704ILR
                : Ayuda_704ILR.TemaServicios_704ILR;

        // La seccion tiene cambios sin guardar si los tiene la ficha del catalogo.
        bool IVistaConCambios_704ILR.HayCambiosSinGuardar_704ILR =>
            _servicios_704ILR != null && !_servicios_704ILR.IsDisposed
            && ((IVistaConCambios_704ILR)_servicios_704ILR).HayCambiosSinGuardar_704ILR;

        // Dejar la pestana del catalogo con datos tipeados sin guardar: se pregunta, con
        // No por defecto, como al cambiar de seccion. Si se sigue, la ficha se descarta.
        // Se usa Deselecting (la pestana que se deja): en Selecting el control ya informa
        // como elegida la pestana nueva. Al liberarse la seccion no se pregunta: frmMain
        // ya pregunto antes de cambiar de seccion o de cerrar.
        private void Tabs_Deselecting_704ILR(object sender_704ILR, TabControlCancelEventArgs e_704ILR)
        {
            if (_preguntando_704ILR || Disposing || IsDisposed || _tabServicios_704ILR == null) return;
            if (e_704ILR.TabPage != _tabServicios_704ILR) return;
            if (!((IVistaConCambios_704ILR)this).HayCambiosSinGuardar_704ILR) return;
            _preguntando_704ILR = true;
            try
            {
                var r_704ILR = MessageBox.Show(FindForm(),
                    T_704ILR("MAIN_CAMBIOS_SIN_GUARDAR", "Hay cambios sin guardar en la sección actual. ¿Descartarlos y continuar?"),
                    "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (r_704ILR != DialogResult.Yes) e_704ILR.Cancel = true;
                // Descartados: la ficha vuelve al catalogo leido de nuevo, asi Salir o el
                // cambio de seccion no vuelven a preguntar por lo que ya se descarto.
                else _servicios_704ILR.Recargar_704ILR();
            }
            finally { _preguntando_704ILR = false; }
        }

        // Al llegar a una pestana se vuelve a leer lo que muestra.
        private void Tabs_Selected_704ILR(object sender_704ILR, TabControlEventArgs e_704ILR)
        {
            if (e_704ILR.TabPage == null) return;
            if (e_704ILR.TabPage == _tabServicios_704ILR) _servicios_704ILR?.Recargar_704ILR();
            else if (e_704ILR.TabPage == _tabCambios_704ILR) _cambios_704ILR?.Refrescar_704ILR();
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        public void ActualizarTextos_704ILR()
        {
            if (_tabServicios_704ILR != null) _tabServicios_704ILR.Text = T_704ILR("MAE_TAB_SERVICIOS", "Servicios y productos");
            if (_tabCambios_704ILR != null) _tabCambios_704ILR.Text = T_704ILR("MAE_TAB_CAMBIOS", "Bitácora de cambios");
        }
    }
}
