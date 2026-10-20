# Reference application review

This is a source-level comparison of the checked-out `lav_sms-master` and
`SkyLearn-main` folders. It records implemented behavior found in routes,
views, controllers, and models; repository descriptions are not treated as proof
that a feature is implemented.

## LAVSMS

### What works well

- Its role predicates distinguish school administrators, teachers, students,
  parents, librarians, and accountants (`lav_sms-master/app/Helpers/Qs.php`).
- Staff navigation covers classes, sections, subjects, exams, grades, and
  marks (`lav_sms-master/resources/views/partials/menu.blade.php`).
- Student records connect a user to class, section, parent, and dorm;
  subjects connect classes and teachers; marks connect student, class,
  section, exam, subject, and grade (`lav_sms-master/app/Models/StudentRecord.php`,
  `Subject.php`, and `Mark.php`).
- Parent routes expose child records and marksheets, and the student menu links
  to marksheets (`lav_sms-master/routes/web.php` and
  `lav_sms-master/resources/views/pages/student/menu.blade.php`).
- A shared application shell is paired with role-conditioned menu partials.
- The dashboard itself contains user-count cards and an events calendar, not
  data charts. Although ECharts assets are bundled in
  `lav_sms-master/public/global_assets/js/plugins/visualization/echarts/`,
  the dashboard template and master layout do not initialize them.

### Limitations observed

- `/`, `/home`, and `/dashboard` all resolve to a shared dashboard view. Admin
  user-count cards are conditional, while the shared page also renders an
  events calendar; this is not a distinct dashboard workflow for every role
  (`lav_sms-master/routes/web.php`,
  `app/Http/Controllers/HomeController.php`, and
  `resources/views/pages/support_team/dashboard.blade.php`).
- No attendance route, model, or view was found in the inspected application
  paths. The presence of exams and marks should not be mistaken for attendance
  management.

## SkyLearn

### What works well

- Dashboard routing is explicitly admin-only, while the root route is
  authenticated News & Events (`SkyLearn-main/core/urls.py` and
  `core/views.py`).
- The admin dashboard uses Chart.js with six canvas elements
  (`SkyLearn-main/templates/core/dashboard.html`).
- Sidebar links are role-conditioned. Lecturers and students get separate
  course, assessment, and result navigation
  (`SkyLearn-main/templates/sidebar.html`).
- Student profiles are one-to-one with users; parents link to students;
  lecturers are allocated courses; student course views derive from the
  logged-in student's registrations (`accounts/models.py`,
  `course/models.py`, and `course/views.py`).
- Course results store assessment scores, assignment, exam, quiz, attendance
  score, total, and grade (`result/models.py`).

### Limitations observed

- The course/program/semester model is different from school classes,
  sections, and subjects, so the two systems are not direct feature-for-feature
  substitutes (`course/models.py`).
- Some dashboard category tiles are hard-coded in the template rather than
  derived from the dashboard view's query results
  (`templates/core/dashboard.html` versus `core/views.py`).
- The Chart.js datasets and `new Chart(...)` initializers are also embedded in
  the dashboard template instead of supplied by the data-backed view. Those
  charts are therefore not a reliable source for real school analytics.
- Attendance appears as a grading component, not a separate attendance
  register. The sidebar has no distinct parent navigation branch.

## EDUCORE implementation choices

EDUCORE keeps its existing server-side school membership and tenant-filtered
student administration. It adds role-protected principal, teacher, and student
dashboards; teacher access is restricted to assigned class rosters, and a
student view is resolved from the authenticated identity's linked student
record. The data-backed principal counts and teacher/student panels only claim
features backed by persisted records. Attendance, timetables, grade entry,
payments, announcements, and learning materials remain explicitly unavailable
until their workflows exist.
