// Platform UI state contains only paging and CSRF information; authorization stays server-side.
const state = {
  csrfToken: "",
  schoolPage: 1,
  userPage: 1,
  loginPage: 1,
  auditPage: 1,
  pageSize: 20,
  historyPageSize: 50
};

const schoolStatuses = ["Active", "Suspended", "Archived"];
const schoolRoles = ["SchoolAdministrator", "Teacher", "Staff", "Student", "Guardian"];

function setupNavigationDrawer() {
  const toggle = document.querySelector("#platform-menu-toggle");
  const sidebar = document.querySelector("#platform-sidebar");
  const backdrop = document.querySelector("#platform-nav-backdrop");
  const main = document.querySelector("main");
  const headerActions = document.querySelector(".top-actions");
  const brand = document.querySelector(".brand");
  const mobile = window.matchMedia("(max-width: 880px)");
  const setOpen = (open, restoreFocus = false) => {
    const isOpen = open && mobile.matches;
    toggle.setAttribute("aria-expanded", String(isOpen));
    toggle.setAttribute("aria-label", isOpen ? "Close navigation" : "Open navigation");
    sidebar.inert = mobile.matches && !isOpen;
    if (main) main.inert = isOpen;
    if (headerActions) headerActions.inert = isOpen;
    if (brand) brand.inert = isOpen;
    backdrop.hidden = !isOpen;
    backdrop.tabIndex = -1;
    document.body.classList.toggle("nav-open", isOpen);
    if (isOpen) sidebar.querySelector("a")?.focus();
    else if (restoreFocus) toggle.focus();
  };

  toggle.addEventListener("click", () =>
    setOpen(toggle.getAttribute("aria-expanded") !== "true"));
  backdrop.addEventListener("click", () => setOpen(false, true));
  sidebar.addEventListener("click", event => {
    const link = event.target.closest("a");
    if (!link || !mobile.matches) return;
    setOpen(false, true);
    if (link.hash) {
      const target = document.querySelector(link.hash);
      if (target) {
        target.tabIndex = -1;
        requestAnimationFrame(() => target.focus({ preventScroll: true }));
      }
    }
  });
  document.addEventListener("keydown", event => {
    if (event.key === "Tab" && toggle.getAttribute("aria-expanded") === "true") {
      const items = [toggle, ...sidebar.querySelectorAll("a[href]:not([hidden])")];
      const index = items.indexOf(document.activeElement);
      if (event.shiftKey && index === 0) {
        event.preventDefault();
        items.at(-1).focus();
      } else if (!event.shiftKey && index === items.length - 1) {
        event.preventDefault();
        items[0].focus();
      }
    }
    if (event.key === "Escape" && toggle.getAttribute("aria-expanded") === "true") {
      setOpen(false, true);
    }
  });
  mobile.addEventListener("change", () => setOpen(false));
  setOpen(false);
}

function updateCurrentNavigation() {
  const activeHash = location.hash || "#overview";
  for (const link of document.querySelectorAll("[data-nav-link]")) {
    if (link.getAttribute("href") === activeHash) {
      link.setAttribute("aria-current", "page");
    } else {
      link.removeAttribute("aria-current");
    }
  }
}

setupNavigationDrawer();
updateCurrentNavigation();
window.addEventListener("hashchange", updateCurrentNavigation);

// Send same-origin requests with CSRF protection and surface API failures to the operator.
async function platformApi(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (options.body) headers.set("Content-Type", "application/json");
  if (state.csrfToken && options.method && options.method !== "GET") {
    headers.set("X-CSRF-TOKEN", state.csrfToken);
  }
  const response = await fetch(path, { ...options, headers, credentials: "same-origin" });
  if (response.status === 401) {
    location.assign("/login.html");
    throw new Error("Please sign in.");
  }
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.detail || body.error || body.title ||
      `Request failed (${response.status}).`);
  }
  return response.status === 204 ? null : response.json();
}

// Display a page-level operation result without inserting untrusted HTML.
function showPlatformMessage(message, isError = false) {
  document.querySelector("#platform-message").textContent = isError ? "" : message;
  document.querySelector("#platform-error").textContent = isError ? message : "";
}

// Convert a value to a table cell using text content to prevent markup injection.
function platformCell(value) {
  const cell = document.createElement("td");
  cell.textContent = value == null ? "" : String(value);
  return cell;
}

function platformStatusCell(value, state) {
  const cell = document.createElement("td");
  const badge = document.createElement("span");
  badge.className = `status-badge is-${state}`;
  badge.textContent = value;
  cell.append(badge);
  return cell;
}

function platformEmptyRow(body, columnCount, message) {
  const row = document.createElement("tr");
  const cell = document.createElement("td");
  cell.className = "empty-row";
  cell.colSpan = columnCount;
  cell.textContent = message;
  row.append(cell);
  body.append(row);
}

// Convert JSON enum values to names while also accepting already-named values.
function enumName(value, names) {
  return typeof value === "number" ? names[value] || `Unknown (${value})` : String(value ?? "");
}

// Show the school administrator role as readable words in the details list.
function schoolRoleLabel(value) {
  const role = enumName(value, schoolRoles);
  return role === "SchoolAdministrator" ? "School administrator" : role;
}

// Format server timestamps consistently as UTC.
function utcTimestamp(value) {
  return value ? new Date(value).toISOString().replace("T", " ").replace("Z", " UTC") : "—";
}

// Build query strings without adding empty search parameters.
function platformQuery(values) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== "" && value != null) params.set(key, value);
  }
  return params.toString();
}

// Attach the common pager controls to a fetched server page.
function configurePager(prefix, result, page, load) {
  document.querySelector(`#${prefix}-page`).textContent =
    `Page ${result.page} of ${Math.max(1, Math.ceil(result.totalCount / result.pageSize))}`;
  document.querySelector(`#${prefix}-previous`).disabled = page <= 1;
  document.querySelector(`#${prefix}-next`).disabled =
    page >= Math.ceil(result.totalCount / result.pageSize);
  document.querySelector(`#${prefix}-previous`).onclick = () => {
    if (page > 1) {
      setPage(prefix, page - 1);
      load();
    }
  };
  document.querySelector(`#${prefix}-next`).onclick = () => {
    if (page < Math.ceil(result.totalCount / result.pageSize)) {
      setPage(prefix, page + 1);
      load();
    }
  };
  document.querySelector(`#${prefix}-page-label`).textContent =
    `${result.totalCount.toLocaleString()} ${result.totalCount === 1 ? "record" : "records"}`;
}

// Update the named pager's in-memory page number.
function setPage(prefix, page) {
  state[`${prefix}Page`] = page;
}

// Load platform totals that deliberately omit student-level and other school records.
async function loadPlatformDashboard() {
  const data = await platformApi("/api/v1/platform/dashboard");
  const metrics = {
    "#metric-schools": data.schoolCount,
    "#metric-active-schools": data.activeSchoolCount,
    "#metric-suspended-schools": data.suspendedSchoolCount,
    "#metric-identities": data.knownIdentityCount,
    "#metric-active-identities": data.activeMembershipIdentityCount,
    "#metric-database": data.databaseConnected ? "Connected" : "Unavailable",
    "#metric-login-success": data.successfulLoginsLastDay,
    "#metric-login-failed": data.failedLoginsLastDay,
    "#metric-audit": data.administrativeChangesLastWeek
  };
  for (const [selector, value] of Object.entries(metrics)) {
    document.querySelector(selector).textContent = Number.isFinite(value) ?
      value.toLocaleString() : value;
  }
}

// Fetch a filtered page of schools and render status controls.
async function loadSchools() {
  const query = platformQuery({
    search: document.querySelector("#school-search-term").value.trim(),
    status: document.querySelector("#school-status-filter").value,
    sort: document.querySelector("#school-sort").value,
    page: state.schoolPage,
    pageSize: state.pageSize
  });
  const result = await platformApi(`/api/v1/platform/schools?${query}`);
  const body = document.querySelector("#schools-table");
  body.replaceChildren();
  for (const school of result.items) {
    const schoolStatus = enumName(school.status, schoolStatuses);
    const row = document.createElement("tr");
    const statusClass = schoolStatus === "Active" ? "active" :
      schoolStatus === "Suspended" ? "warning" : "neutral";
    row.append(
      platformCell(school.name),
      platformCell(school.slug),
      platformStatusCell(schoolStatus, statusClass),
      platformCell(utcTimestamp(school.createdUtc)),
      platformCell(school.membershipCount));
    const actions = document.createElement("td");
    const details = document.createElement("button");
    details.className = "button-secondary";
    details.type = "button";
    details.textContent = "Details";
    details.addEventListener("click", () => runPlatformAction(() => showSchoolDetails(school.id)));
    const status = document.createElement("button");
    status.className = "button-secondary";
    status.type = "button";
    status.textContent = schoolStatus === "Active" ? "Suspend" : "Reactivate";
    status.disabled = schoolStatus === "Archived";
    status.addEventListener("click", () => runPlatformAction(async () => {
      const nextStatus = schoolStatus === "Active" ? "Suspended" : "Active";
      if (nextStatus === "Suspended" &&
          !confirm(`Suspend ${school.name}? This blocks new school operations but preserves its data.`)) {
        return;
      }
      await platformApi(`/api/v1/platform/schools/${school.id}/status`, {
        method: "PUT",
        body: JSON.stringify({ status: nextStatus })
      });
      showPlatformMessage(`${school.name} is now ${nextStatus.toLowerCase()}.`);
      await Promise.all([loadSchools(), loadPlatformDashboard()]);
    }));
    actions.append(details, status);
    row.append(actions);
    body.append(row);
  }
  if (!result.items.length) platformEmptyRow(body, 6, "No schools match these filters.");
  configurePager("schools", result, state.schoolPage, loadSchools);
}

// Load a school's safe administrative details without requesting its student records.
async function showSchoolDetails(schoolId) {
  const details = await platformApi(`/api/v1/platform/schools/${schoolId}`);
  const container = document.querySelector("#school-details");
  container.replaceChildren();
  container.hidden = false;
  const heading = document.createElement("h3");
  heading.textContent = `${details.name} (${details.slug})`;
  const status = document.createElement("p");
  status.textContent =
    `Status: ${enumName(details.status, schoolStatuses)} · Registered: ${utcTimestamp(details.createdUtc)}`;
  const list = document.createElement("ul");
  for (const membership of details.membershipCounts) {
    const item = document.createElement("li");
    item.textContent =
      `${schoolRoleLabel(membership.role)}: ${membership.active} active of ${membership.total} memberships`;
    list.append(item);
  }
  const close = document.createElement("button");
  close.type = "button";
  close.className = "button-secondary";
  close.textContent = "Close details";
  close.addEventListener("click", () => { container.hidden = true; });
  container.append(heading, status, list, close);
}

// Search and render platform-known identity records and school-local memberships.
async function loadUsers() {
  const query = platformQuery({
    search: document.querySelector("#user-search-term").value.trim(),
    page: state.userPage,
    pageSize: state.pageSize
  });
  const result = await platformApi(`/api/v1/platform/users?${query}`);
  const body = document.querySelector("#users-table");
  body.replaceChildren();
  for (const user of result.items) {
    const row = document.createElement("tr");
    const identity = document.createElement("td");
    const login = document.createElement("strong");
    login.textContent = user.loginIdentifier;
    const subject = document.createElement("small");
    subject.textContent = `Subject: ${user.subjectId}`;
    identity.append(login, document.createElement("br"), subject);
    row.append(identity, platformCell(user.accountStatus));

    const memberships = document.createElement("td");
    if (!user.memberships.length) {
      memberships.textContent = "No school memberships";
    }
    for (const membership of user.memberships) {
      const editor = document.createElement("div");
      editor.className = "membership-editor";
      const label = document.createElement("label");
      label.textContent = `${membership.schoolName} — role`;
      const select = document.createElement("select");
      const membershipRole = enumName(membership.role, schoolRoles);
      select.setAttribute("aria-label", `Role for ${user.subjectId} at ${membership.schoolName}`);
      for (const role of ["SchoolAdministrator", "Teacher", "Staff", "Student", "Guardian"]) {
        const option = document.createElement("option");
        option.value = role;
        option.textContent = role === "SchoolAdministrator" ? "School administrator" : role;
        option.selected = role === membershipRole;
        select.append(option);
      }
      const activeLabel = document.createElement("label");
      const active = document.createElement("input");
      active.type = "checkbox";
      active.checked = membership.isActive;
      active.setAttribute("aria-label",
        `Active membership for ${user.subjectId} at ${membership.schoolName}`);
      activeLabel.append(active, document.createTextNode(" Active"));
      const save = document.createElement("button");
      save.type = "button";
      save.className = "button-secondary";
      save.textContent = "Save membership";
      save.addEventListener("click", () => runPlatformAction(async () => {
        await platformApi(`/api/v1/platform/memberships/${membership.id}`, {
          method: "PUT",
          body: JSON.stringify({ role: select.value, isActive: active.checked })
        });
        showPlatformMessage("Membership updated and audited.");
        await loadUsers();
      }));
      label.append(select);
      editor.append(label, activeLabel, save);
      memberships.append(editor);
    }
    row.append(memberships);
    const permissions = document.createElement("td");
    permissions.textContent = user.permissions.length ?
      user.permissions.join(", ") : "None";
    row.append(permissions);
    body.append(row);
  }
  if (!result.items.length) platformEmptyRow(body, 4, "No identities match this search.");
  configurePager("users", result, state.userPage, loadUsers);
}

// List only capabilities explicitly delegated by a platform roles manager.
async function loadPermissions() {
  const grants = await platformApi("/api/v1/platform/permissions");
  const body = document.querySelector("#permissions-table");
  body.replaceChildren();
  for (const grant of grants) {
    const row = document.createElement("tr");
    row.append(platformCell(grant.subjectId), platformCell(grant.permission));
    const action = document.createElement("td");
    const revoke = document.createElement("button");
    revoke.type = "button";
    revoke.className = "button-secondary";
    revoke.textContent = grant.permission === "platform.roles.manage" ?
      "Bootstrap-only" : "Revoke";
    revoke.disabled = grant.permission === "platform.roles.manage";
    revoke.addEventListener("click", () => runPlatformAction(async () => {
      if (!confirm(`Revoke ${grant.permission} from ${grant.subjectId}?`)) return;
      await platformApi(`/api/v1/platform/permissions/${grant.id}`, { method: "DELETE" });
      showPlatformMessage("Permission revoked and audited.");
      await Promise.all([loadPermissions(), loadUsers(), loadPlatformDashboard()]);
    }));
    action.append(revoke);
    row.append(action);
    body.append(row);
  }
  if (!grants.length) platformEmptyRow(body, 3, "No delegated permissions are available.");
}

// Load a page of EduCore-managed authentication attempts.
async function loadLoginHistory() {
  const query = platformQuery({
    search: document.querySelector("#login-search-term").value.trim(),
    page: state.loginPage,
    pageSize: state.historyPageSize
  });
  const result = await platformApi(`/api/v1/platform/login-history?${query}`);
  const body = document.querySelector("#login-table");
  body.replaceChildren();
  for (const item of result.items) {
    const row = document.createElement("tr");
    row.append(
      platformCell(utcTimestamp(item.createdUtc)),
      platformCell(item.accountIdentifier),
      platformStatusCell(item.succeeded ? "Success" : "Failure",
        item.succeeded ? "active" : "error"),
      platformCell(item.outcome));
    body.append(row);
  }
  if (!result.items.length) platformEmptyRow(body, 4, "No login events match this search.");
  configurePager("login", result, state.loginPage, loadLoginHistory);
}

// Load the immutable platform-administration audit stream.
async function loadAuditEvents() {
  const query = platformQuery({
    search: document.querySelector("#audit-search-term").value.trim(),
    page: state.auditPage,
    pageSize: state.historyPageSize
  });
  const result = await platformApi(`/api/v1/platform/audit-events?${query}`);
  const body = document.querySelector("#audit-table");
  body.replaceChildren();
  for (const item of result.items) {
    const row = document.createElement("tr");
    row.append(
      platformCell(utcTimestamp(item.createdUtc)),
      platformCell(item.actorSubjectId),
      platformCell(item.action),
      platformCell(`${item.targetType}: ${item.targetId || ""}`),
      platformCell(item.details));
    body.append(row);
  }
  if (!result.items.length) platformEmptyRow(body, 5, "No audit events match this search.");
  configurePager("audit", result, state.auditPage, loadAuditEvents);
}

// Run one UI action and report failures rather than hiding rejected API operations.
async function runPlatformAction(action) {
  try {
    showPlatformMessage("");
    await action();
  } catch (error) {
    showPlatformMessage(error.message, true);
  }
}

// Initialize only after the server confirms the caller can see the platform dashboard.
(async () => {
  try {
    const csrf = await fetch("/api/v1/auth/csrf", { credentials: "same-origin" });
    if (csrf.ok) {
      state.csrfToken = (await csrf.json()).token;
    }
    await loadPlatformDashboard();
    await Promise.all([
      loadSchools(),
      loadUsers(),
      loadPermissions(),
      loadLoginHistory(),
      loadAuditEvents()
    ]);
  } catch (error) {
    showPlatformMessage(error.message, true);
  }
})();

// Use server-side filters and reset to the first result page after each search.
for (const [formId, prefix, loader] of [
  ["school-search", "school", loadSchools],
  ["user-search", "user", loadUsers],
  ["login-search", "login", loadLoginHistory],
  ["audit-search", "audit", loadAuditEvents]
]) {
  document.querySelector(`#${formId}`).addEventListener("submit", event => {
    event.preventDefault();
    setPage(prefix, 1);
    runPlatformAction(loader);
  });
}

// Register a school only after the server validates its name, identifier, and slug uniqueness.
document.querySelector("#school-create-form").addEventListener("submit", event => {
  event.preventDefault();
  runPlatformAction(async () => {
    const name = document.querySelector("#new-school-name").value.trim();
    const slug = document.querySelector("#new-school-slug").value.trim();
    await platformApi("/api/v1/platform/schools", {
      method: "POST",
      body: JSON.stringify({ name, slug })
    });
    event.currentTarget.reset();
    showPlatformMessage("School registered and audited.");
    setPage("school", 1);
    await Promise.all([loadSchools(), loadPlatformDashboard()]);
  });
});

// Assign a school-only role; the backend records the change and blocks self-assignment.
document.querySelector("#membership-create-form").addEventListener("submit", event => {
  event.preventDefault();
  runPlatformAction(async () => {
    const schoolId = document.querySelector("#membership-school-id").value.trim();
    const subjectId = document.querySelector("#membership-subject-id").value.trim();
    const role = document.querySelector("#membership-role").value;
    await platformApi(`/api/v1/platform/schools/${encodeURIComponent(schoolId)}/memberships`, {
      method: "POST",
      body: JSON.stringify({ subjectId, role })
    });
    event.currentTarget.reset();
    showPlatformMessage("School membership assigned and audited.");
    setPage("user", 1);
    await Promise.all([loadUsers(), loadSchools()]);
  });
});

// Grant only server-allowlisted platform permissions; self-grants are rejected by the API.
document.querySelector("#grant-form").addEventListener("submit", event => {
  event.preventDefault();
  runPlatformAction(async () => {
    await platformApi("/api/v1/platform/permissions", {
      method: "POST",
      body: JSON.stringify({
        subjectId: document.querySelector("#grant-subject").value.trim(),
        permission: document.querySelector("#grant-permission").value
      })
    });
    event.currentTarget.reset();
    showPlatformMessage("Permission granted and audited.");
    await Promise.all([loadPermissions(), loadUsers(), loadPlatformDashboard()]);
  });
});

// Reauthenticate this local account's current password; the server signs it out after a change.
document.querySelector("#self-password-form").addEventListener("submit", event => {
  event.preventDefault();
  runPlatformAction(async () => {
    await platformApi("/api/v1/auth/change-password", {
      method: "POST",
      body: JSON.stringify({
        currentPassword: document.querySelector("#current-password").value,
        newPassword: document.querySelector("#new-password").value
      })
    });
    location.assign("/login.html");
  });
});

// Keep provider-backed password, MFA, and session actions with Keycloak.
for (const [buttonId, route, successMessage] of [
  ["identity-recovery", "password-recovery", "Password recovery email requested from Keycloak."],
  ["identity-mfa", "mfa-enrollment", "TOTP enrollment email requested from Keycloak."],
  ["identity-revoke", "sessions/revoke", "Keycloak sessions and refresh tokens revoked."]
]) {
  document.querySelector(`#${buttonId}`).addEventListener("click", () => runPlatformAction(async () => {
    const subjectId = document.querySelector("#identity-subject-id").value.trim();
    await platformApi(
      `/api/v1/platform/users/${encodeURIComponent(subjectId)}/${route}`,
      { method: "POST" });
    showPlatformMessage(successMessage);
    await loadAuditEvents();
  }));
}

// Sign out through the existing CSRF-protected authentication endpoint.
document.querySelector("#platform-logout").addEventListener("click", () => runPlatformAction(async () => {
  await platformApi("/api/v1/auth/logout", { method: "POST" });
  location.assign("/login.html");
}));
