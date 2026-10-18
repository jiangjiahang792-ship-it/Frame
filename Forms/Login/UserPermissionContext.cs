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
        /// 是否允许使用手动调参与一键学习。厂家继承高级权限能力。
        /// </summary>
        public static bool CanUseManualTuning
        {
            get { return CanUseAdvancedFeatures; }
        }

        /// <summary>高级和厂家均可使用原有高级功能，不依赖角色枚举的数值大小。</summary>
        public static bool CanUseAdvancedFeatures => CurrentRole == UserRole.Hight || CurrentRole == UserRole.Manufacturer;

        /// <summary>高级及厂家权限允许进入AI训练。</summary>
        public static bool CanTrainAi => CanUseAdvancedFeatures;

        /// <summary>仅厂家允许编辑布局、关闭或隐藏窗格，以及拖动停靠和浮动窗口。</summary>
        public static bool CanEditWorkspaceLayout => CurrentRole == UserRole.Manufacturer;

        /// <summary>初级与高级权限允许保存当前方案，中级维持原有的禁止保存策略。</summary>
        public static bool CanSaveSolution => CurrentRole == UserRole.Low || CanUseAdvancedFeatures;

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
