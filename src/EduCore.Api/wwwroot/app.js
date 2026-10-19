// Client state is limited to the current page; the server cookie remains the authority for identity and school.
let csrfToken;
let currentPage = 1;
const pageSize = 10;
let activeSchoolId;

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
        await loadStudents();
      } catch (error) {
        showFormMessage(error.message);
      }
    });
    buttons.append(edit, remove);
    actions.append(buttons);
    row.append(idCell, nameCell, actions);
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
        document.querySelector("#school-title").textContent = session.schoolName || "Your school";
        document.querySelector("#current-user").textContent = `${session.userName} · ${session.role}`;
      } else {
        document.querySelector("#school-title").textContent = "Choose your school";
      }

      if (activeSchoolId) {
        await loadStudents();
      }
    } catch (error) {
      showFormMessage(error.message);
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
    try {
      await api(id ? `/api/v1/students/${id}` : "/api/v1/students", {
        method: id ? "PUT" : "POST", body
      });
      clearStudentForm();
      await loadStudents();
    } catch (error) {
      showFormMessage(error.message);
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
