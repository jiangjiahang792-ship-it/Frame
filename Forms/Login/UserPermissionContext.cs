using System;

namespace TDJS_Vision.Forms.Login
{
    /// <summary>
    /// 当前登录用户权限上下文，统一向主窗口和已创建的子窗口发布角色变化。
    /// </summary>
    public static class UserPermissionContext
    {
        /// <summary>
        /// 当前登录角色。软件启动时按初级权限处理，避免窗口初始化阶段短暂开放受限功能。
        /// </summary>
        public static UserRole CurrentRole { get; private set; } = UserRole.Low;

        /// <summary>
        /// 登录角色发生变化时触发。
        /// </summary>
        public static event EventHandler<UserRole> RoleChanged;

        /// <summary>
        /// 是否允许使用手动调参与一键学习。工艺约定仅最高权限允许使用。
        /// </summary>
        public static bool CanUseManualTuning
        {
            get { return CurrentRole == UserRole.Hight; }
        }

        /// <summary>
        /// 更新当前角色并通知所有已创建窗口立即刷新权限状态。
        /// </summary>
        /// <param name="role">新的登录角色。</param>
        public static void UpdateRole(UserRole role)
        {
            if (CurrentRole == role)
                return;

            CurrentRole = role;
            RoleChanged?.Invoke(null, role);
        }
    }
}
