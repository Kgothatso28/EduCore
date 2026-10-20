// Client state is limited to the current page; the server cookie remains the authority for identity and school.
let csrfToken;
let currentPage = 1;
const pageSize = 10;
let activeSchoolId;

function setupNavigationDrawer(toggleId, sidebarId, backdropId) {
  const toggle = document.querySelector(`#${toggleId}`);
  const sidebar = document.querySelector(`#${sidebarId}`);
  const backdrop = document.querySelector(`#${backdropId}`);
  const main = document.querySelector("main");
  const headerActions = document.querySelector(".top-actions");
  const brand = document.querySelector(".brand");
  if (!toggle || !sidebar || !backdrop) return;

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

function updateWorkspaceNavigation(role) {
  for (const link of document.querySelectorAll("[data-nav-role]")) {
    link.hidden = link.dataset.navRole !== role;
  }
  updateCurrentNavigation();
}

function updateCurrentNavigation() {
  const activeHash = location.hash || "#school-title";
  for (const link of document.querySelectorAll("[data-nav-link]")) {
    if (link.getAttribute("href") === activeHash) {
      link.setAttribute("aria-current", "page");
    } else {
      link.removeAttribute("aria-current");
    }
  }
}

setupNavigationDrawer("menu-toggle", "app-sidebar", "nav-backdrop");
window.addEventListener("hashchange", updateCurrentNavigation);

// Centralize same-origin API calls, CSRF headers, authentication redirects, and error conversion.
async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (options.body) headers.set("Content-Type", "application/json");
  if (csrfToken && options.method && options.method !== "GET") {
    headers.set("X-CSRF-TOKEN", csrfToken);
  }
  const response = await fetch(path, { ...options, headers, credentials: "same-origin" });
  if (response.status === 401) {
    location.assign("/login.html");
    throw new Error("Please sign in.");
  }
  if (!response.ok) {
    // Some authorization and validation errors are empty; retain the HTTP status as a useful fallback.
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.detail || problem.error || `Request failed (${response.status}).`);
  }
  return response.status === 204 ? null : response.json();
}

// Fetch and cache the antiforgery token required by cookie-authenticated write requests.
async function refreshCsrf() {
  csrfToken = (await api("/api/v1/auth/csrf")).token;
}

const loginForm = document.querySelector("#login-form");
if (loginForm) {
  document.querySelector("#toggle-password").addEventListener("click", event => {
    const password = document.querySelector("#password");
    const visible = password.type === "password";
    password.type = visible ? "text" : "password";
    event.currentTarget.textContent = visible ? "Hide" : "Show";
    event.currentTarget.setAttribute("aria-pressed", String(visible));
  });

  // Authenticate submitted credentials, then send the signed-in user to the school dashboard.
  loginForm.addEventListener("submit", async event => {
    event.preventDefault();
    const message = document.querySelector("#message");
    message.textContent = "";
    try {
      await refreshCsrf();
      await api("/api/v1/auth/login", {
        method: "POST",
        body: JSON.stringify({
          userName: loginForm.elements.userName.value,
          password: loginForm.elements.password.value
        })
      });
      location.assign("/");
    } catch (error) {
      message.textContent = error.message === "Request failed (401)." ?
        "Sign-in failed. Check your username and password." : error.message;
    }
  });
}

// Fetch the current page of tenant-scoped students and render rows plus pagination state.
async function loadStudents() {
  const search = document.querySelector("#search").value.trim();
  const query = new URLSearchParams({ page: currentPage, pageSize, search });
  const result = await api(`/api/v1/students?${query}`);
  const rows = document.querySelector("#students");
  rows.replaceChildren();
  for (const student of result.items) {
    const row = document.createElement("tr");
    const idCell = document.createElement("td");
    idCell.textContent = student.localId;
    const nameCell = document.createElement("td");
    nameCell.textContent = `${student.firstName} ${student.lastName}`;
    const actions = document.createElement("td");
    const buttons = document.createElement("div");
    buttons.className = "row-actions";
    const edit = document.createElement("button");
    edit.className = "button-secondary";
    edit.type = "button";
    edit.textContent = "Edit";
    // Load this row's values into the shared student form for editing.
    edit.addEventListener("click", () => startEdit(student));
    const remove = document.createElement("button");
    remove.className = "button-secondary";
    remove.type = "button";
    remove.textContent = "Delete";
    remove.setAttribute("aria-label", `Delete ${student.firstName} ${student.lastName}`);
    // Require confirmation before deleting, then refresh the visible page.
    remove.addEventListener("click", async () => {
      if (!confirm(`Delete ${student.firstName} ${student.lastName}?`)) return;
      try {
        await api(`/api/v1/students/${student.id}`, { method: "DELETE" });
          await refreshPrincipalData();
      } catch (error) {
        showFormMessage(error.message);
      }
    });
    buttons.append(edit, remove);
    actions.append(buttons);
    row.append(idCell, nameCell, actions);
    rows.append(row);
  }
  if (!result.items.length) {
    const row = document.createElement("tr");
    const empty = document.createElement("td");
    empty.className = "empty-row";
    empty.colSpan = 3;
    empty.textContent = search ? "No students match this search." : "No student records yet.";
    row.append(empty);
    rows.append(row);
  }
  document.querySelector("#student-count").textContent = `${result.totalCount} students`;
  document.querySelector("#page-label").textContent = `Page ${result.page} of ${Math.max(1, result.totalPages)}`;
  document.querySelector("#previous-page").disabled = result.page <= 1;
  document.querySelector("#next-page").disabled = result.page >= result.totalPages;
}

// Populate the shared form with a selected student's current values.
function startEdit(student) {
  document.querySelector("#student-id").value = student.id;
  document.querySelector("#local-id").value = student.localId;
  document.querySelector("#first-name").value = student.firstName;
  document.querySelector("#last-name").value = student.lastName;
  document.querySelector("#save-student").textContent = "Save changes";
  document.querySelector("#cancel-edit").hidden = false;
  document.querySelector("#local-id").focus();
}

// Reset the shared form to create mode after a save or cancellation.
function clearStudentForm() {
  document.querySelector("#student-form").reset();
  document.querySelector("#student-id").value = "";
  document.querySelector("#save-student").textContent = "Add student";
  document.querySelector("#cancel-edit").hidden = true;
  showFormMessage("");
}

// Show form-level feedback without interrupting the current page.
function showFormMessage(message) {
  document.querySelector("#form-message").textContent = message;
}

// Render named value bars; the visible list gives the same data without relying on color or hover.
function renderBarChart(container, title, items, valueName) {
  container.replaceChildren();
  if (!items.length) {
    const empty = document.createElement("p");
    empty.className = "chart-empty";
    empty.textContent = "No records are available for this chart.";
    container.append(empty);
    return;
  }

  const figure = document.createElement("figure");
  figure.className = "chart-figure";
  const caption = document.createElement("figcaption");
  caption.textContent = `${title}. Values are ${valueName}.`;
  const list = document.createElement("ol");
  list.className = "bar-chart";
  const maxValue = Math.max(1, items.reduce((max, item) => Math.max(max, item.value), 0));
  for (const item of items) {
    const row = document.createElement("li");
    row.className = "bar-row";
    row.title = `${item.label}: ${item.value} ${valueName}`;
    const label = document.createElement("span");
    label.className = "bar-label";
    label.textContent = item.label;
    const track = document.createElement("span");
    track.className = "bar-track";
    track.setAttribute("aria-hidden", "true");
    const fill = document.createElement("span");
    fill.className = "bar-fill";
    fill.style.width = `${item.value / maxValue * 100}%`;
    track.append(fill);
    const value = document.createElement("span");
    value.className = "bar-value";
    value.textContent = item.value.toLocaleString();
    row.append(label, track, value);
    list.append(row);
  }
  figure.append(caption, list);
  container.append(figure);
}

// Render the real monthly student-record cohorts as a responsive, accessible SVG line chart.
function renderLineChart(container, title, points) {
  container.replaceChildren();
  const maxValue = points.reduce((max, point) => Math.max(max, point.count), 0);
  if (!points.length || maxValue === 0) {
    const empty = document.createElement("p");
    empty.className = "chart-empty";
    empty.textContent = "No current student records were created in this year.";
    container.append(empty);
    return;
  }

  const svgNamespace = "http://www.w3.org/2000/svg";
  const width = 760;
  const height = 290;
  const left = 52;
  const right = 18;
  const top = 22;
  const bottom = 50;
  const plotWidth = width - left - right;
  const plotHeight = height - top - bottom;
  const scaleMax = Math.max(1, Math.ceil(maxValue / 4) * 4);
  const x = index => left + index * plotWidth / Math.max(1, points.length - 1);
  const y = value => top + plotHeight - value / scaleMax * plotHeight;
  const svg = document.createElementNS(svgNamespace, "svg");
  svg.setAttribute("class", "line-chart");
  svg.setAttribute("viewBox", `0 0 ${width} ${height}`);
  svg.setAttribute("role", "img");
  svg.setAttribute("aria-labelledby", "enrolment-svg-title enrolment-svg-description");
  const svgTitle = document.createElementNS(svgNamespace, "title");
  svgTitle.id = "enrolment-svg-title";
  svgTitle.textContent = title;
  const description = document.createElementNS(svgNamespace, "desc");
  description.id = "enrolment-svg-description";
  description.textContent =
    "Monthly counts of currently retained student records by their creation date. Deleted records are not available.";
  svg.append(svgTitle, description);

  for (const tick of [...new Set([0, Math.ceil(scaleMax / 2), scaleMax])]) {
    const yPosition = y(tick);
    const grid = document.createElementNS(svgNamespace, "line");
    grid.setAttribute("x1", left);
    grid.setAttribute("x2", width - right);
    grid.setAttribute("y1", yPosition);
    grid.setAttribute("y2", yPosition);
    grid.setAttribute("class", "line-grid");
    svg.append(grid);
    const label = document.createElementNS(svgNamespace, "text");
    label.setAttribute("x", left - 10);
    label.setAttribute("y", yPosition + 4);
    label.setAttribute("text-anchor", "end");
    label.setAttribute("class", "line-axis-label");
    label.textContent = tick.toLocaleString();
    svg.append(label);
  }

  const path = document.createElementNS(svgNamespace, "path");
  path.setAttribute("class", "line-series");
  path.setAttribute("d", points.map((point, index) =>
    `${index ? "L" : "M"} ${x(index)} ${y(point.count)}`).join(" "));
  svg.append(path);

  points.forEach((point, index) => {
    const circle = document.createElementNS(svgNamespace, "circle");
    circle.setAttribute("class", "line-point");
    circle.setAttribute("cx", x(index));
    circle.setAttribute("cy", y(point.count));
    circle.setAttribute("r", "5");
    const tooltip = document.createElementNS(svgNamespace, "title");
    tooltip.textContent = `${point.label}: ${point.count} current student records`;
    circle.append(tooltip);
    svg.append(circle);

    const monthLabel = document.createElementNS(svgNamespace, "text");
    monthLabel.setAttribute("x", x(index));
    monthLabel.setAttribute("y", height - 25);
    monthLabel.setAttribute("text-anchor", "middle");
    monthLabel.setAttribute("class", "line-axis-label");
    monthLabel.textContent = point.label;
    svg.append(monthLabel);
  });

  const figure = document.createElement("figure");
  figure.className = "chart-figure";
  const caption = document.createElement("figcaption");
  caption.textContent =
    "Creation month (horizontal axis); current student records created (vertical axis).";
  const table = document.createElement("table");
  table.className = "sr-only";
  const tableCaption = document.createElement("caption");
  tableCaption.textContent = title;
  const head = document.createElement("thead");
  const headerRow = document.createElement("tr");
  for (const text of ["Month", "Current records created"]) {
    const cell = document.createElement("th");
    cell.scope = "col";
    cell.textContent = text;
    headerRow.append(cell);
  }
  head.append(headerRow);
  const body = document.createElement("tbody");
  for (const point of points) {
    const row = document.createElement("tr");
    const month = document.createElement("th");
    month.scope = "row";
    month.textContent = point.label;
    const count = document.createElement("td");
    count.textContent = point.count.toLocaleString();
    row.append(month, count);
    body.append(row);
  }
  table.append(tableCaption, head, body);
  figure.append(caption, svg, table);
  container.append(figure);
}

// Refresh principal counts and chart series using only the validated active-school endpoint.
async function loadPrincipalAnalytics() {
  const yearSelector = document.querySelector("#analytics-year");
  const enrolmentNote = document.querySelector("#enrolment-note");
  const classNote = document.querySelector("#class-count-note");
  const enrolmentChart = document.querySelector("#enrolment-chart");
  const classChart = document.querySelector("#class-count-chart");
  const selectedYear = yearSelector.value;
  enrolmentNote.textContent = "Loading enrolment data…";
  classNote.textContent = "Loading class data…";
  enrolmentChart.setAttribute("aria-busy", "true");
  classChart.setAttribute("aria-busy", "true");
  enrolmentChart.replaceChildren();
  classChart.replaceChildren();

  try {
    const analytics = await api(
      `/api/v1/dashboard/principal/analytics?year=${encodeURIComponent(selectedYear)}`);
    document.querySelector("#metric-students").textContent =
      analytics.currentStudentCount.toLocaleString();
    document.querySelector("#metric-teachers").textContent =
      analytics.teacherCount.toLocaleString();
    document.querySelector("#metric-classes").textContent =
      analytics.classCount.toLocaleString();
    document.querySelector("#metric-assignments").textContent =
      analytics.assignmentCount.toLocaleString();
    enrolmentNote.textContent = analytics.enrollmentLimitation;
    classNote.textContent = analytics.studentsByClass.length ?
      "Counts include unassigned students when present." : "No classes or assigned students are recorded.";
    renderLineChart(enrolmentChart, `Student records created in ${analytics.year}`,
      analytics.monthlyEnrollments);
    renderBarChart(classChart, "Current students by class",
      analytics.studentsByClass.map(item => ({ label: item.label, value: item.count })),
      "students");
  } catch (error) {
    enrolmentNote.textContent = `Enrolment analytics failed to load: ${error.message}`;
    classNote.textContent = `Class analytics failed to load: ${error.message}`;
  } finally {
    enrolmentChart.removeAttribute("aria-busy");
    classChart.removeAttribute("aria-busy");
  }
}

// Keep principal charts and totals in sync after a student is created or removed.
async function refreshPrincipalData() {
  await Promise.all([loadStudents(), loadPrincipalAnalytics()]);
}

// Load the class roster only for a class returned in this teacher's authorized dashboard.
async function showTeacherRoster(schoolClass) {
  const message = document.querySelector("#teacher-message");
  message.textContent = "";
  try {
    const students = await api(
      `/api/v1/dashboard/teacher/classes/${schoolClass.id}/students`);
    const rows = document.querySelector("#roster-students");
    rows.replaceChildren();
    for (const student of students) {
      const row = document.createElement("tr");
      const id = document.createElement("td");
      id.textContent = student.localId;
      const name = document.createElement("td");
      name.textContent = `${student.firstName} ${student.lastName}`;
      row.append(id, name);
      rows.append(row);
    }
    document.querySelector("#roster-title").textContent =
      `${schoolClass.name} · ${schoolClass.subjectName}`;
    document.querySelector("#class-roster").hidden = false;
  } catch (error) {
    message.textContent = error.message;
  }
}

const studentForm = document.querySelector("#student-form");
if (studentForm) {
  // Initialize antiforgery, resolve authorized schools, activate a sole school, then load its data.
  (async () => {
    try {
      await refreshCsrf();
      const schools = await api("/api/v1/auth/schools");
      const selector = document.querySelector("#school-select");
      // Convert only server-authorized schools into choices; the option list does not grant access.
      selector.replaceChildren(...schools.map(school => {
        const option = document.createElement("option");
        option.value = school.id;
        option.textContent = school.name;
        option.selected = school.id === activeSchoolId;
        return option;
      }));
      // Persist a user-selected school in the authentication cookie before reloading tenant data.
      selector.addEventListener("change", async () => {
        try {
          await api("/api/v1/auth/school", {
            method: "POST",
            body: JSON.stringify({ schoolId: selector.value })
          });
          location.reload();
        } catch (error) {
          showFormMessage(error.message);
          selector.value = activeSchoolId || "";
        }
      });

      if (schools.length === 0) {
        const response = await fetch("/api/v1/platform/dashboard", {
          credentials: "same-origin"
        });
        if (response.ok) {
          location.assign("/platform.html");
          return;
        }
        if (response.status === 401) {
          location.assign("/login.html");
          return;
        }
        document.querySelector("#school-title").textContent = "No school access";
        document.querySelector("#dashboard-description").textContent =
          "This account has no active school membership or platform dashboard permission.";
        return;
      }

      // Activate a sole authorized school before requesting session data, avoiding an expected pre-selection 403.
      if (schools.length === 1) {
        selector.value = schools[0].id;
        await api("/api/v1/auth/school", {
          method: "POST",
          body: JSON.stringify({ schoolId: selector.value })
        });
        activeSchoolId = schools[0].id;
      }

      if (activeSchoolId || schools.length > 1) {
        const session = await api("/api/v1/session");
        activeSchoolId = session.schoolId;
        selector.value = activeSchoolId || "";
        document.querySelector("#school-title").textContent = session.schoolName || "Your school";
        document.querySelector("#current-user").textContent = `${session.userName} · ${session.role}`;
        document.querySelector("#role-label").textContent =
          session.role === "SchoolAdministrator" ? "PRINCIPAL" : session.role.toUpperCase();
        updateWorkspaceNavigation(session.role);
        const description = document.querySelector("#dashboard-description");
        const principal = session.role === "SchoolAdministrator";
        const teacher = session.role === "Teacher";
        const student = session.role === "Student";
        document.querySelector("#principal-dashboard").hidden = !principal;
        document.querySelector("#principal-analytics").hidden = !principal;
        document.querySelector("#student-management").hidden = !principal;
        document.querySelector("#teacher-dashboard").hidden = !teacher;
        document.querySelector("#student-dashboard").hidden = !student;
        document.querySelector("#planned-modules").hidden = !(principal || teacher || student);

        if (principal) {
          description.textContent = "Principal overview and school-wide student record management.";
          document.querySelector("#planned-message").textContent =
            "The overview and student directory are available. Other modules remain planned.";
          const yearSelector = document.querySelector("#analytics-year");
          const currentYear = new Date().getUTCFullYear();
          yearSelector.replaceChildren(...Array.from({ length: 5 }, (_, index) => {
            const year = currentYear - index;
            const option = document.createElement("option");
            option.value = year;
            option.textContent = year;
            return option;
          }));
          yearSelector.value = currentYear;
          yearSelector.addEventListener("change", () => {
            loadPrincipalAnalytics();
          });
          await refreshPrincipalData();
        } else if (teacher) {
          description.textContent = "Your assigned classes, subjects, and student rosters.";
          document.querySelector("#planned-message").textContent =
            "Attendance, assessment results, grading, and marking workflows are not yet implemented.";
          const dashboard = await api("/api/v1/dashboard/teacher");
          document.querySelector("#teacher-name").textContent = `Welcome, ${dashboard.name}`;
          document.querySelector("#teacher-class-count").textContent =
            dashboard.assignedClassCount.toLocaleString();
          document.querySelector("#teacher-subject-count").textContent =
            dashboard.assignedSubjectCount.toLocaleString();
          document.querySelector("#teacher-student-count").textContent =
            dashboard.assignedStudentCount.toLocaleString();
          document.querySelector("#teacher-class-note").textContent =
            dashboard.studentsByClass.length ?
              "Students in classes assigned to your teacher account." :
              "No classes are assigned to your account.";
          renderBarChart(
            document.querySelector("#teacher-class-chart"),
            "Students by assigned class",
            dashboard.studentsByClass.map(item => ({
              label: item.label,
              value: item.studentCount
            })),
            "students");
          const cards = document.querySelector("#teacher-classes");
          cards.replaceChildren();
          if (!dashboard.classes.length) {
            const empty = document.createElement("p");
            empty.className = "empty-state-note";
            empty.setAttribute("role", "status");
            empty.textContent = "No classes are assigned to your account yet.";
            cards.append(empty);
          }
          for (const schoolClass of dashboard.classes) {
            const card = document.createElement("article");
            card.className = "class-card";
            const heading = document.createElement("h3");
            heading.textContent = schoolClass.name;
            const details = document.createElement("p");
            details.textContent =
              `${schoolClass.gradeLevel} · ${schoolClass.subjectName} · ${schoolClass.studentCount} students`;
            const roster = document.createElement("button");
            roster.type = "button";
            roster.textContent = "View roster";
            roster.addEventListener("click", () => showTeacherRoster(schoolClass));
            card.append(heading, details, roster);
            cards.append(card);
          }
        } else if (student) {
          description.textContent = "Your linked student profile and class subjects.";
          document.querySelector("#planned-message").textContent =
            "EduCore does not yet store assessment results, grades, or attendance. No academic or attendance charts can be calculated.";
          const profile = await api("/api/v1/dashboard/student");
          document.querySelector("#profile-id").textContent = profile.localId;
          document.querySelector("#profile-name").textContent =
            `${profile.firstName} ${profile.lastName}`;
          document.querySelector("#profile-class").textContent = profile.className || "Not assigned";
          document.querySelector("#profile-grade").textContent = profile.gradeLevel || "Not assigned";
          const subjects = document.querySelector("#profile-subjects");
          subjects.replaceChildren();
          for (const subject of profile.subjects) {
            const item = document.createElement("li");
            item.textContent = subject;
            subjects.append(item);
          }
          document.querySelector("#student-message").textContent = profile.subjects.length ?
            "" : "No subjects have been assigned to your class yet.";
        } else {
          description.textContent = "This school role does not have a dashboard in this release.";
          document.querySelector("#planned-modules").hidden = false;
          document.querySelector("#planned-message").textContent =
            "A role-specific dashboard is not available for this account yet.";
        }
      } else {
        document.querySelector("#school-title").textContent = "Choose your school";
      }
    } catch (error) {
      const dashboardMessage = document.querySelector("#dashboard-message");
      if (dashboardMessage) {
        dashboardMessage.textContent = error.message;
      }
      const message = document.querySelector("#form-message");
      if (message) {
        showFormMessage(error.message);
      }
      const teacherMessage = document.querySelector("#teacher-message");
      if (teacherMessage && !document.querySelector("#teacher-dashboard").hidden) {
        teacherMessage.textContent = error.message;
      }
      const studentMessage = document.querySelector("#student-message");
      if (studentMessage && !document.querySelector("#student-dashboard").hidden) {
        studentMessage.textContent = error.message;
      }
    }
  })();

  // Send create/update data without accepting a school ID from the browser.
  studentForm.addEventListener("submit", async event => {
    event.preventDefault();
    showFormMessage("");
    const id = document.querySelector("#student-id").value;
    const body = JSON.stringify({
      localId: document.querySelector("#local-id").value,
      firstName: document.querySelector("#first-name").value,
      lastName: document.querySelector("#last-name").value
    });
    const saveButton = document.querySelector("#save-student");
    saveButton.disabled = true;
    saveButton.textContent = "Saving…";
    try {
      await api(id ? `/api/v1/students/${id}` : "/api/v1/students", {
        method: id ? "PUT" : "POST", body
      });
      clearStudentForm();
      await refreshPrincipalData();
    } catch (error) {
      showFormMessage(error.message);
    } finally {
      saveButton.disabled = false;
      saveButton.textContent = document.querySelector("#student-id").value ?
        "Save changes" : "Add student";
    }
  });

  // Discard current edits and return to create mode.
  document.querySelector("#cancel-edit").addEventListener("click", clearStudentForm);
  // Start a new search from the first result page.
  document.querySelector("#search-form").addEventListener("submit", event => {
    event.preventDefault();
    currentPage = 1;
    loadStudents().catch(error => showFormMessage(error.message));
  });
  // Move to the previous page and report load errors in the form status.
  document.querySelector("#previous-page").addEventListener("click", () => {
    currentPage--;
    loadStudents().catch(error => showFormMessage(error.message));
  });
  // Move to the next page and report load errors in the form status.
  document.querySelector("#next-page").addEventListener("click", () => {
    currentPage++;
    loadStudents().catch(error => showFormMessage(error.message));
  });
  // End the cookie session and return to sign-in.
  document.querySelector("#logout").addEventListener("click", async () => {
    try {
      await api("/api/v1/auth/logout", { method: "POST" });
      location.assign("/login.html");
    } catch (error) {
      showFormMessage(error.message);
    }
  });
}
