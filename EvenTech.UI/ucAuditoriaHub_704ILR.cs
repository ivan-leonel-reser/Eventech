using System;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Seccion de Auditoria unificada: bitacora general + auditoria de login en
    // pestanas (antes eran dos items separados del menu). Observa el idioma para
    // traducir los titulos de las pestanas; cada UserControl interno se traduce solo.
    // Incluye la accion administrativa "Recalcular linea base" (T08): reestablece
    // los digitos verificadores tras una correccion de datos, solo para quien
    // tenga el permiso INTEGRIDAD_RECALC (el perfil Administrador lo trae).
    public class ucAuditoriaHub_704ILR : UserControl, IObservadorIdioma_704ILR
    {
        private readonly TabControl _tabs_704ILR;
        private readonly TabPage _tabBitacora_704ILR;   // null si el usuario no tiene BITACORA_VER
        private readonly TabPage _tabLogin_704ILR;      // null si el usuario no tiene AUDIT_LOGIN_VER
        private readonly AppButton_704ILR _btnRecalc_704ILR;   // null si el usuario no tiene permiso
        private readonly Label _lblSinConsulta_704ILR;   // solo cuando no hay ninguna pestana

        public ucAuditoriaHub_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;

            _tabs_704ILR = new TabControl { Dock = DockStyle.Fill, Font = Theme_704ILR.FontBody_704ILR };

            // La seccion se abre con CUALQUIERA de los dos permisos, asi que cada
            // pestana se agrega solo si el usuario tiene el suyo: tener uno no
            // puede conceder el contenido del otro (la bitacora general y la
            // auditoria de login exponen datos distintos).
            if (Permisos_704ILR.Tiene_704ILR("BITACORA_VER"))
            {
                _tabBitacora_704ILR = new TabPage { BackColor = Theme_704ILR.BgContent_704ILR, Padding = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0), UseVisualStyleBackColor = true };
                _tabBitacora_704ILR.Controls.Add(new ucBitacora_704ILR { Dock = DockStyle.Fill });
                _tabs_704ILR.TabPages.Add(_tabBitacora_704ILR);
            }
            if (Permisos_704ILR.Tiene_704ILR("AUDIT_LOGIN_VER"))
            {
                _tabLogin_704ILR = new TabPage { BackColor = Theme_704ILR.BgContent_704ILR, Padding = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0), UseVisualStyleBackColor = true };
                _tabLogin_704ILR.Controls.Add(new ucAuditoria_704ILR { Dock = DockStyle.Fill });
                _tabs_704ILR.TabPages.Add(_tabLogin_704ILR);
            }
            Controls.Add(_tabs_704ILR);
            // La seccion tambien se abre con solo INTEGRIDAD_RECALC (el recalculo de
            // la linea base vive aca). Sin ninguna pestana, en lugar de un control de
            // pestanas vacio se explica que la consulta no esta habilitada.
            if (_tabs_704ILR.TabPages.Count == 0)
            {
                _tabs_704ILR.Visible = false;
                _lblSinConsulta_704ILR = new Label
                {
                    Dock = DockStyle.Fill, Font = Theme_704ILR.FontBody_704ILR, ForeColor = Theme_704ILR.TextMuted_704ILR,
                    TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
                };
                Controls.Add(_lblSinConsulta_704ILR);
            }

            if (Permisos_704ILR.Tiene_704ILR("INTEGRIDAD_RECALC"))
            {
                var toolbar_704ILR = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Theme_704ILR.BgContent_704ILR };
                _btnRecalc_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("AUD_RECALC_BTN", "Recalcular línea base"));
                _btnRecalc_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
                _btnRecalc_704ILR.Size = new Size(240, 38);
                _btnRecalc_704ILR.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                _btnRecalc_704ILR.Location = new Point(toolbar_704ILR.Width - _btnRecalc_704ILR.Width - Theme_704ILR.SpaceLg_704ILR,
                                                toolbar_704ILR.Height - _btnRecalc_704ILR.Height - Theme_704ILR.SpaceSm_704ILR);
                _btnRecalc_704ILR.Click += (s_704ILR, e_704ILR) => RecalcularLineaBase_704ILR();
                toolbar_704ILR.Controls.Add(_btnRecalc_704ILR);
                Controls.Add(toolbar_704ILR);
            }

            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        // Proceso ante corrupcion (T08): el administrador corrige los datos (o
        // restaura una version) y desde aca reestablece la linea base de DV.
        private void RecalcularLineaBase_704ILR()
        {
            // Segunda capa: el boton solo se crea con permiso, pero la accion
            // vuelve a exigirlo antes de reescribir la linea base de integridad.
            if (!Permisos_704ILR.Exigir_704ILR("INTEGRIDAD_RECALC", FindForm(), "recalcular la linea base de DV")) return;

            // La linea base nueva reemplaza a la vigente como referencia de integridad: la pregunta
            // arranca en No, como las demas confirmaciones que reemplazan o descartan datos.
            var confirma_704ILR = MessageBox.Show(
                T_704ILR("AUD_RECALC_CONFIRMA",
                  "¿Recalcular los dígitos verificadores de todas las reservas y de todos los pagos? Usar después de corregir datos alterados: la línea base nueva pasa a ser la referencia de integridad."),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (confirma_704ILR != DialogResult.Yes) return;

            try
            {
                int total_704ILR = BLL_Integridad_704ILR.RecalcularTodo_704ILR();
                var resultado_704ILR = BLL_Integridad_704ILR.Verificar_704ILR();
                MessageBox.Show(
                    Tr_704ILR.F_704ILR("AUD_RECALC_OK",
                        "Línea base recalculada ({0} reservas, con sus pagos). Verificación posterior: {1} inconsistencia(s).",
                        total_704ILR, resultado_704ILR.Inconsistencias_704ILR.Count),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Integridad", "Recalculo de linea base");
                MessageBox.Show(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), T_704ILR("MSG_ERROR", "Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Devuelve la traduccion de 'clave' o, si falta, el texto por defecto dado.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        public void ActualizarTextos_704ILR()
        {
            if (_tabBitacora_704ILR != null) _tabBitacora_704ILR.Text = T_704ILR("AUD_TAB_BITACORA", "Bitácora general");
            if (_tabLogin_704ILR != null) _tabLogin_704ILR.Text = T_704ILR("AUD_TAB_LOGIN", "Auditoría de login");
            if (_btnRecalc_704ILR != null) _btnRecalc_704ILR.Text = T_704ILR("AUD_RECALC_BTN", "Recalcular línea base");
            if (_lblSinConsulta_704ILR != null)
                _lblSinConsulta_704ILR.Text = T_704ILR("AUD_SIN_CONSULTA", "El perfil no tiene permisos de consulta de auditoría.");
        }
    }
}
