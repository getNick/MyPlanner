import React, { useEffect, useState } from "react";
import { useLocation, Outlet, NavLink } from "react-router-dom";
import {
  LayoutDashboard,
  Clock,
  TrendingUp,
  Landmark,
  Receipt,
  Menu,
  X,
} from "lucide-react";
import { UserButton, useUser } from "@clerk/clerk-react";

interface NavItem {
  id: string;
  label: string;
  path: string;
  icon: React.ElementType;
}

const NAV_ITEMS: NavItem[] = [
  { id: "home", label: "Home", path: "/", icon: LayoutDashboard },
  { id: "todo", label: "TODO", path: "/todo", icon: Clock },
  { id: "finance", label: "Finance", path: "/finance", icon: TrendingUp },
  { id: "bank", label: "Bank", path: "/finance/bank", icon: Landmark },
  {
    id: "shopping-bills",
    label: "Shopping Bills",
    path: "/finance/shopping-bills",
    icon: Receipt,
  },
];

type SidebarVariant = "desktop" | "drawer";

interface SidebarPanelProps {
  variant: SidebarVariant;
  onClose?: () => void;
  className?: string;
}

// One panel definition shared by the in-flow sidebar and the overlay drawer.
// The desktop variant collapses to an icon rail below the lg breakpoint.
const SidebarPanel: React.FC<SidebarPanelProps> = ({
  variant,
  onClose,
  className = "",
}) => {
  const { isLoaded, user } = useUser();
  const isDrawer = variant === "drawer";
  const avatarSize = isDrawer ? "w-8 h-8 text-xs" : "w-10 h-10 text-sm";

  return (
    <aside
      className={`flex flex-col bg-zinc-950 text-zinc-300 border-r border-zinc-800 ${className}`}
    >
      {isDrawer && onClose && (
        <div className="flex justify-end shrink-0 p-3">
          <button
            onClick={onClose}
            aria-label="Close menu"
            className="p-1 text-zinc-400 hover:text-white"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
      )}

      {/* Navigation scrolls independently of the account control. */}
      <nav className="flex-1 min-h-0 py-4 overflow-y-auto">
        <ul className="space-y-1 px-2">
          {NAV_ITEMS.map((item) => {
            const Icon = item.icon;
            return (
              <li key={item.id}>
                <NavLink
                  to={item.path}
                  title={isDrawer ? undefined : item.label}
                  onClick={onClose}
                  className={({ isActive }) =>
                    `w-full flex items-center space-x-2.5 px-3 py-2.5 rounded-sm text-sm font-mono no-underline transition-colors text-left ${
                      isDrawer ? "" : "justify-center lg:justify-start"
                    } ${
                      isActive
                        ? "bg-zinc-100 text-zinc-950 font-bold border-l-2"
                        : "text-zinc-400 hover:text-zinc-100 hover:bg-zinc-900"
                    }`
                  }
                >
                  <Icon className="w-4 h-4 shrink-0" />
                  <span className={isDrawer ? "" : "hidden lg:inline"}>
                    {item.label}
                  </span>
                </NavLink>
              </li>
            );
          })}
        </ul>
      </nav>

      {/* Bottom section: Clerk profile management and sign-out. */}
      <div
        className={`shrink-0 border-t border-zinc-800 ${
          isDrawer ? "p-4" : "p-2 lg:p-4"
        }`}
      >
        <div
          className={`flex items-center gap-3 ${
            isDrawer ? "" : "justify-center lg:justify-start"
          }`}
        >
          {isLoaded && user ? (
            <div className="shrink-0">
              <UserButton
                appearance={{
                  elements: {
                    avatarBox: `${avatarSize} rounded-sm border-2 border-zinc-600`,
                    userButtonPopoverCard: "z-[60]",
                  },
                }}
                userProfileProps={{
                  appearance: {
                    elements: { modalBackdrop: "z-[60]" },
                  },
                }}
              />
            </div>
          ) : (
            <div
              className={`${avatarSize} rounded-sm bg-zinc-700 flex items-center justify-center font-bold font-mono shrink-0`}
            >
              ?
            </div>
          )}
          <div
            className={`flex-1 min-w-0 ${isDrawer ? "" : "hidden lg:block"}`}
          >
            <div className="text-sm font-semibold truncate font-mono">
              {isLoaded && user ? user.fullName || "User" : "Loading..."}
            </div>
            {isLoaded && user && (
              <div className="text-xs text-zinc-400 truncate font-mono">
                {user.primaryEmailAddress?.emailAddress || ""}
              </div>
            )}
          </div>
        </div>
      </div>
    </aside>
  );
};

const LayoutShell: React.FC = () => {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const location = useLocation();

  // The drawer is an overlay, so it must get out of the way after navigating.
  useEffect(() => {
    setDrawerOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!drawerOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setDrawerOpen(false);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [drawerOpen]);

  return (
    <div className="flex h-screen w-full bg-slate-100 text-zinc-900 font-sans selection:bg-zinc-900 selection:text-white">
      {/* Sidebar: part of the flex flow, so it pushes content instead of covering it */}
      <SidebarPanel
        variant="desktop"
        className="hidden md:flex w-16 lg:w-64 shrink-0"
      />

      {/* Overlay drawer: only below md, where there is no room for a rail */}
      {drawerOpen && (
        <div
          className="fixed inset-0 bg-zinc-950/70 backdrop-blur-xs z-40 md:hidden"
          onClick={() => setDrawerOpen(false)}
        />
      )}
      <div
        className={`fixed inset-y-0 left-0 z-50 transform transition-transform duration-200 ease-in-out md:hidden ${
          drawerOpen ? "translate-x-0" : "-translate-x-full pointer-events-none"
        }`}
      >
        <SidebarPanel
          variant="drawer"
          onClose={() => setDrawerOpen(false)}
          className="h-full w-72 max-w-[85vw] relative"
        />
      </div>

      {/* Main content area */}
      <div className="flex-1 flex flex-col min-w-0 overflow-y-auto bg-slate-50">
        {/* Drawer toggle button */}
        <button
          onClick={() => setDrawerOpen(true)}
          className="md:hidden fixed top-3 left-3 z-30 p-2 rounded-sm bg-slate-100 hover:bg-slate-200 border border-slate-300 text-slate-800"
          aria-label="Open menu"
          aria-expanded={drawerOpen}
        >
          <Menu className="w-5 h-5" />
        </button>

        {/* Page content - no top bar, routes render directly */}
        <div className="p-3 sm:p-6 max-w-[1600px] mx-auto w-full flex-1">
          <Outlet />
        </div>
      </div>
    </div>
  );
};

export default LayoutShell;
