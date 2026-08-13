using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace APDP.Controllers
{
    public class ChatbotController : Controller
    {
        // ── POST /Chatbot/GetResponse ────────────────────────────────
        [HttpPost]
        public JsonResult GetResponse(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return Json(new { reply = "Please type a message and I'll help you! 😊" });

            string reply = HarbourBot.GetReply(message.Trim(), Session);
            return Json(new { reply });
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  HarbourBot — rule-based knowledge engine
    // ════════════════════════════════════════════════════════════════
    public static class HarbourBot
    {
        public static string GetReply(string input, System.Web.HttpSessionStateBase session)
        {
            string msg = input.ToLower();

            // ── Greetings ────────────────────────────────────────────
            if (Matches(msg, "hello", "hi", "hey", "good morning", "good afternoon",
                              "good evening", "howzit", "hola", "sup", "greetings"))
            {
                string name = GetUserName(session);
                return string.IsNullOrEmpty(name)
                    ? "👋 Hello! Welcome to HarbourConnect. I'm your virtual assistant. How can I help you today?\n\nYou can ask me about:\n• Registering or logging in\n• Booking a boat\n• TNPA approvals\n• Managing boats\n• Driver portal\n• Or anything else about the app!"
                    : $"👋 Hello {name}! Great to see you. What can I help you with today?";
            }

            // ── How are you ──────────────────────────────────────────
            if (Matches(msg, "how are you", "how r u", "you ok", "hows it going"))
                return "I'm doing great, thank you for asking! 😊 I'm here and ready to help you navigate HarbourConnect. What would you like to do?";

            // ── What is HarbourConnect ───────────────────────────────
            if (Matches(msg, "what is harbourconnect", "about harbourconnect",
                              "what is this", "what does this app do", "what is this website",
                              "explain harbourconnect", "tell me about"))
                return "⚓ <strong>HarbourConnect</strong> is an online platform that connects:\n\n🚢 <strong>Boat Owners</strong> — Register and manage their boats\n👪 <strong>Customers</strong> — Browse and book harbour trips\n📋 <strong>TNPA Admins</strong> — Approve or reject boat registrations\n🚗 <strong>Drivers</strong> — View their trip schedule\n\nAll boats must be approved by TNPA before customers can book them. This ensures safety and compliance at the harbour.";

            // ── Registration ─────────────────────────────────────────
            if (Matches(msg, "register", "sign up", "create account", "new account",
                              "how to register", "how do i register", "registration"))
            {
                if (Matches(msg, "boat owner", "owner"))
                    return "🚢 <strong>To register as a Boat Owner:</strong>\n\n1. Click <strong>Register</strong> in the top navbar\n2. Choose <strong>Register as Boat Owner</strong>\n3. Fill in:\n   • Full Name\n   • Business Name\n   • Phone Number\n   • Email Address\n   • Password (min 6 characters)\n4. Click <strong>Create Account</strong>\n5. You'll be redirected to the Login page\n\n<a href='/BoatOwner/Register' class='btn btn-sm btn-primary mt-2'>Register as Boat Owner</a>";

                if (Matches(msg, "customer", "user", "passenger", "book"))
                    return "👪 <strong>To register as a Customer:</strong>\n\n1. Click <strong>Register</strong> in the top navbar\n2. Choose <strong>Register as Customer</strong>\n3. Fill in:\n   • Full Name\n   • Phone Number\n   • Email Address\n   • Password (min 6 characters)\n4. Click <strong>Create Account</strong>\n5. You can then login and start booking boats!\n\n<a href='/Customer/Register' class='btn btn-sm btn-success mt-2'>Register as Customer</a>";

                return "📝 <strong>Who would you like to register as?</strong>\n\n• <a href='/BoatOwner/Register'>Boat Owner</a> — Register and manage boats\n• <a href='/Customer/Register'>Customer</a> — Browse and book harbour trips\n\nDrivers are registered by their Boat Owner. TNPA Admins have pre-set accounts.";
            }

            // ── Login ────────────────────────────────────────────────
            if (Matches(msg, "login", "log in", "sign in", "how to login",
                              "cant login", "cannot login", "forgot", "password"))
            {
                if (Matches(msg, "boat owner", "owner"))
                    return "🚢 <strong>Boat Owner Login:</strong>\n\n1. Click <strong>Login</strong> in the navbar\n2. Choose <strong>Boat Owner Login</strong>\n3. Enter your email and password\n4. Click <strong>Login</strong>\n\n<a href='/BoatOwner/Login' class='btn btn-sm btn-primary mt-2'>Boat Owner Login</a>";

                if (Matches(msg, "customer", "passenger"))
                    return "👪 <strong>Customer Login:</strong>\n\n1. Click <strong>Login</strong> in the navbar\n2. Choose <strong>Customer Login</strong>\n3. Enter your email and password\n\n<a href='/Customer/Login' class='btn btn-sm btn-success mt-2'>Customer Login</a>";

                if (Matches(msg, "tnpa", "admin", "authority"))
                    return "📋 <strong>TNPA Admin Login:</strong>\n\nUse these credentials:\n• Email: <strong>admin@tnpa.co.za</strong>\n• Password: <strong>Admin@123</strong>\n\n<a href='/Tnpa/Login' class='btn btn-sm btn-dark mt-2'>TNPA Login</a>";

                if (Matches(msg, "driver"))
                    return "🚗 <strong>Driver Login:</strong>\n\nDemo credentials:\n• Email: <strong>driver@demo.co.za</strong>\n• Password: <strong>Driver@123</strong>\n\n<a href='/Driver/Login' class='btn btn-sm btn-success mt-2'>Driver Login</a>";

                if (Matches(msg, "forgot", "reset", "password"))
                    return "🔑 <strong>Forgot your password?</strong>\n\nCurrently, password reset is managed by the system administrator. Please contact support at:\n📧 support@harbourconnect.co.za\n📞 +27 31 555 0100";

                return "🔐 <strong>Which portal would you like to login to?</strong>\n\n• <a href='/BoatOwner/Login'>Boat Owner Login</a>\n• <a href='/Customer/Login'>Customer Login</a>\n• <a href='/Driver/Login'>Driver Login</a>\n• <a href='/Tnpa/Login'>TNPA Admin Login</a>";
            }

            // ── Booking ──────────────────────────────────────────────
            if (Matches(msg, "book", "booking", "reserve", "trip", "how to book",
                              "make a booking", "book a boat"))
            {
                if (Matches(msg, "cancel", "cancell"))
                    return "❌ <strong>How to Cancel a Booking:</strong>\n\n1. Login as a Customer\n2. Go to <strong>My Bookings</strong>\n3. Find the booking you want to cancel\n4. Click <strong>Cancel Booking</strong>\n\n⚠️ Note: Bookings can only be cancelled more than 24 hours before the trip date.";

                if (Matches(msg, "my booking", "view booking", "see booking"))
                    return "📋 <strong>View Your Bookings:</strong>\n\n1. Login as a Customer\n2. Click <strong>My Bookings</strong> in your dashboard\n3. You'll see all your bookings with status, trip date, and total price\n\n<a href='/Customer/MyBookings' class='btn btn-sm btn-info mt-2'>My Bookings</a>";

                return "📅 <strong>How to Book a Boat:</strong>\n\n1. <a href='/Customer/Login'>Login</a> as a Customer\n2. Click <strong>Browse Boats</strong> from your dashboard\n3. Filter by location or boat type if needed\n4. Click <strong>Book Now</strong> on any boat\n5. You'll see the <strong>live weather</strong> for that location\n6. Choose your trip date and number of passengers\n7. Click <strong>Confirm Booking</strong>\n\nYour booking will be confirmed immediately! 🎉";
            }

            // ── Boats ────────────────────────────────────────────────
            if (Matches(msg, "add boat", "register boat", "new boat", "submit boat"))
                return "🚢 <strong>How to Add a Boat:</strong>\n\n1. Login as a Boat Owner\n2. Go to your <strong>Dashboard</strong>\n3. Click <strong>Add New Boat</strong>\n4. Fill in all the details:\n   • Boat Name & Registration Number\n   • Boat Type & Harbour Location\n   • Max Passengers & Price per Trip\n   • Life Jackets, Features (Fishing, Sound System, Decoration)\n   • Upload a Boat Image\n5. Click <strong>Submit for TNPA Approval</strong>\n\n⏳ The boat will be <strong>Pending</strong> until TNPA reviews it.";

            if (Matches(msg, "edit boat", "update boat", "change boat"))
                return "✏️ <strong>How to Edit a Boat:</strong>\n\n1. Login as a Boat Owner\n2. Go to <strong>My Boats</strong>\n3. Click <strong>Edit</strong> next to the boat\n4. Update the information\n5. Click <strong>Save Changes</strong>\n\n⚠️ If you change the Boat Name or Registration Number, the boat status will reset to <strong>Pending</strong> for TNPA re-review.";

            if (Matches(msg, "my boat", "view boat", "boat list", "boat status"))
                return "🚢 <strong>View Your Boats:</strong>\n\n1. Login as a Boat Owner\n2. Click <strong>My Boats</strong> on your dashboard\n3. You'll see all boats with their:\n   • Name, Type, Location\n   • Status: Approved ✅ / Pending ⏳ / Rejected ❌\n   • Price and Max Passengers\n\n<a href='/BoatOwner/MyBoats' class='btn btn-sm btn-primary mt-2'>My Boats</a>";

            // ── Boat Status ──────────────────────────────────────────
            if (Matches(msg, "pending", "approved", "rejected", "approval", "boat approved",
                              "why is my boat", "boat not approved", "status"))
                return "📊 <strong>Boat Status Explained:</strong>\n\n⏳ <strong>Pending</strong> — Your boat has been submitted and is waiting for TNPA to review it.\n\n✅ <strong>Approved</strong> — TNPA has approved your boat. Customers can now book it!\n\n❌ <strong>Rejected</strong> — TNPA has rejected your boat. A reason will be shown. You can edit the boat and resubmit.\n\nFor approval questions, contact TNPA at admin@tnpa.co.za";

            // ── TNPA ─────────────────────────────────────────────────
            if (Matches(msg, "tnpa", "transnet", "authority", "approve", "reject",
                              "pending boat", "review boat"))
                return "📋 <strong>TNPA Admin Functions:</strong>\n\nTNPA Admins can:\n• View all <strong>Pending boat applications</strong>\n• View full boat details (specs, owner info, image)\n• <strong>Approve</strong> boats — they become available for booking\n• <strong>Reject</strong> boats with a reason\n• View <strong>All Boats</strong> and their status\n• See stats on the dashboard\n\n<a href='/Tnpa/Login' class='btn btn-sm btn-dark mt-2'>TNPA Portal</a>";

            // ── Driver ───────────────────────────────────────────────
            if (Matches(msg, "driver", "drive", "trip schedule", "assigned boat",
                              "my trips", "driver portal", "driver dashboard"))
                return "🚗 <strong>Driver Portal:</strong>\n\nDrivers can:\n• Login at <a href='/Driver/Login'>/Driver/Login</a>\n• View their <strong>Dashboard</strong> — assigned boat info and stats\n• See <strong>My Trips</strong> — all upcoming confirmed bookings for their boat\n• View passenger details, trip dates and special requests\n• Check their <strong>Profile</strong> — license, boat owner, assignment details\n\nDrivers are assigned to boats by their Boat Owner.\n\nDemo: driver@demo.co.za / Driver@123";

            // ── Weather ──────────────────────────────────────────────
            if (Matches(msg, "weather", "wind", "rain", "temperature", "sailing condition",
                              "is it safe", "safe to sail", "forecast"))
                return "🌤️ <strong>Weather on HarbourConnect:</strong>\n\nWhen you open the <strong>Book a Boat</strong> page, you'll automatically see:\n\n• 🌡️ Current temperature & feels like\n• 💧 Humidity\n• 💨 Wind speed (km/h)\n• ☁️ Weather description with icon\n• ⛵ <strong>Sailing Conditions advice</strong>:\n  - 🟢 Good Conditions — safe to sail\n  - 🟡 Moderate — sail with caution\n  - 🔴 Dangerous — do not sail\n\nWeather data is provided by OpenWeatherMap and updates in real time.";

            // ── Dashboard ────────────────────────────────────────────
            if (Matches(msg, "dashboard", "home page", "my dashboard", "what can i do"))
            {
                string userType = session["UserType"] as string;
                if (userType == "BoatOwner")
                    return "🚢 <strong>Your Boat Owner Dashboard includes:</strong>\n\n• 📊 Stats — Total Boats, Approved, Pending, Bookings\n• 🚢 My Boats — view all your registered boats\n• ➕ Add New Boat — submit a boat for TNPA approval\n• 📋 Bookings — see all customer bookings for your boats\n\n<a href='/BoatOwner/Dashboard' class='btn btn-sm btn-primary mt-2'>Go to Dashboard</a>";

                if (userType == "TnpaAdmin")
                    return "📋 <strong>Your TNPA Dashboard includes:</strong>\n\n• 📊 Stats — Pending, Approved, Rejected, Total Owners\n• ⏳ Pending Boats — review and approve/reject applications\n• 🚢 All Boats — view every registered boat\n\n<a href='/Tnpa/Dashboard' class='btn btn-sm btn-dark mt-2'>Go to Dashboard</a>";

                if (userType == "Customer")
                    return "👪 <strong>Your Customer Dashboard includes:</strong>\n\n• 📊 Stats — Available Boats, My Bookings\n• 🚢 Browse Boats — find and book harbour trips\n• 📋 My Bookings — manage your reservations\n\n<a href='/Customer/Dashboard' class='btn btn-sm btn-success mt-2'>Go to Dashboard</a>";

                if (userType == "Driver")
                    return "🚗 <strong>Your Driver Dashboard includes:</strong>\n\n• 📊 Stats — Upcoming Trips, Total Trips\n• 🚢 Assigned Boat details\n• 📅 My Trips — full trip schedule\n• 👤 My Profile — license and assignment info\n\n<a href='/Driver/Dashboard' class='btn btn-sm btn-success mt-2'>Go to Dashboard</a>";

                return "📊 <strong>Dashboards by user type:</strong>\n\n🚢 <a href='/BoatOwner/Dashboard'>Boat Owner Dashboard</a> — manage boats and bookings\n👪 <a href='/Customer/Dashboard'>Customer Dashboard</a> — browse and book trips\n📋 <a href='/Tnpa/Dashboard'>TNPA Dashboard</a> — approve/reject boats\n🚗 <a href='/Driver/Dashboard'>Driver Dashboard</a> — view trip schedule";
            }

            // ── Logout ───────────────────────────────────────────────
            if (Matches(msg, "logout", "log out", "sign out", "exit"))
                return "👋 <strong>To Logout:</strong>\n\nClick the <strong>Logout</strong> button in the top navigation bar, or use the button on your dashboard.\n\nYou'll be redirected to the login page.";

            // ── Contact ──────────────────────────────────────────────
            if (Matches(msg, "contact", "support", "help", "phone", "email address",
                              "reach", "get in touch"))
                return "📞 <strong>Contact HarbourConnect:</strong>\n\n📧 General: info@harbourconnect.co.za\n📧 Boat Owners: owners@harbourconnect.co.za\n📧 Customer Support: support@harbourconnect.co.za\n📞 Phone: +27 31 555 0100\n\n📍 Durban Harbour, KwaZulu-Natal, South Africa\n\n<a href='/Home/Contact' class='btn btn-sm btn-outline-primary mt-2'>Contact Page</a>";

            // ── Pricing ──────────────────────────────────────────────
            if (Matches(msg, "price", "cost", "how much", "fee", "charge", "rate", "rand"))
                return "💰 <strong>Pricing on HarbourConnect:</strong>\n\nEach boat has its own price set by the Boat Owner. The price is shown as <strong>R per trip</strong>.\n\nWhen booking:\n• Total price = Price per trip × Number of passengers\n• You'll see the total before confirming\n\nTo see current prices, <a href='/Customer/Login'>login as a Customer</a> and browse available boats.";

            // ── Locations ────────────────────────────────────────────
            if (Matches(msg, "location", "harbour", "where", "durban", "cape town",
                              "port elizabeth", "east london", "richards bay"))
                return "📍 <strong>HarbourConnect Harbour Locations:</strong>\n\n• 🌊 Durban Harbour, KwaZulu-Natal\n• 🌊 Cape Town Harbour, Western Cape\n• 🌊 Port Elizabeth (Gqeberha), Eastern Cape\n• 🌊 East London, Eastern Cape\n• 🌊 Richards Bay, KwaZulu-Natal\n\nYou can filter boats by location when browsing as a customer.";

            // ── Boat types ───────────────────────────────────────────
            if (Matches(msg, "boat type", "type of boat", "fishing boat", "yacht",
                              "leisure", "speed boat", "catamaran", "party boat"))
                return "🚢 <strong>Boat Types on HarbourConnect:</strong>\n\n• 🎣 Fishing Boat\n• ⛵ Leisure Cruiser\n• 🚤 Speed Boat\n• ⛵ Yacht\n• ⛵ Catamaran\n• 🎉 Party Boat\n• 🚢 Other\n\nYou can filter by boat type when browsing as a customer.";

            // ── Life jackets / safety ────────────────────────────────
            if (Matches(msg, "life jacket", "safety", "safe", "equipment", "how safe"))
                return "🦺 <strong>Safety on HarbourConnect:</strong>\n\nAll boats are required to list:\n• Number of life jackets on board\n• Whether fishing equipment is available\n• Sound system and decoration features\n\nAll boats must pass <strong>TNPA review</strong> before they can accept bookings. The TNPA admin checks the registration details and safety compliance before approving any vessel.";

            // ── Image upload ─────────────────────────────────────────
            if (Matches(msg, "image", "photo", "picture", "upload", "boat image"))
                return "📸 <strong>Boat Image Upload:</strong>\n\nWhen adding or editing a boat, you can upload a boat image.\n\n• Accepted formats: JPG, JPEG, PNG, GIF\n• The image appears on the boat listing card for customers\n• If no image is uploaded, a default boat icon is shown\n\nImages are stored in the <strong>Content/BoatImages</strong> folder on the server.";

            // ── Demo accounts ────────────────────────────────────────
            if (Matches(msg, "demo", "test account", "test login", "demo account",
                              "sample", "example login", "credentials"))
                return "🔑 <strong>Demo Accounts for Testing:</strong>\n\n| User | Email | Password |\n|------|-------|----------|\n| TNPA Admin | admin@tnpa.co.za | Admin@123 |\n| Boat Owner | owner@demo.co.za | Owner@123 |\n| Customer | customer@demo.co.za | Customer@123 |\n| Driver | driver@demo.co.za | Driver@123 |\n\nUse these to test all features of the system.";

            // ── Database ─────────────────────────────────────────────
            if (Matches(msg, "database", "sql", "entity framework", "data", "stored",
                              "where is data", "saved"))
                return "🗄️ <strong>HarbourConnect Database:</strong>\n\nThe app uses <strong>SQL Server LocalDB</strong> with <strong>Entity Framework 6 Code First</strong>.\n\nTables:\n• BoatOwners\n• Boats\n• Customers\n• Bookings\n• TnpaAdmins\n• Drivers\n\nThe database is automatically created when you run the app. You can view it in Visual Studio under <strong>View → SQL Server Object Explorer</strong>.";

            // ── Technical / build errors ─────────────────────────────
            if (Matches(msg, "error", "bug", "not working", "broken", "crash",
                              "exception", "problem", "issue"))
                return "🔧 <strong>Having a technical issue?</strong>\n\nTry these steps:\n\n1. Check the <strong>Error List</strong> in Visual Studio (View → Error List)\n2. Press <strong>Ctrl+Shift+B</strong> to rebuild\n3. Stop and restart with <strong>Ctrl+F5</strong>\n4. Check the error message in the browser\n\nCommon fixes:\n• Database error → Delete HarbourConnectDB in SQL Server Object Explorer and restart\n• Build error → Check the Error List for the specific file and line\n• Login error → Make sure you're using the correct demo credentials\n\nFor persistent issues, contact support@harbourconnect.co.za";

            // ── Thank you ────────────────────────────────────────────
            if (Matches(msg, "thank", "thanks", "thank you", "thx", "dankie", "appreciated"))
                return "😊 You're welcome! I'm always here if you need help. Is there anything else I can assist you with?";

            // ── Bye ──────────────────────────────────────────────────
            if (Matches(msg, "bye", "goodbye", "see you", "cya", "later", "exit"))
                return "👋 Goodbye! Have a wonderful day and enjoy your harbour experience! ⚓";

            // ── Who are you ──────────────────────────────────────────
            if (Matches(msg, "who are you", "what are you", "your name", "are you a bot",
                              "are you human", "are you ai", "chatbot"))
                return "🤖 I'm <strong>HarbourBot</strong>, the virtual assistant for HarbourConnect!\n\nI know everything about this app — from registering and booking boats to TNPA approvals, driver trips, and more.\n\nJust ask me anything about HarbourConnect and I'll guide you! 😊";

            // ── Help ─────────────────────────────────────────────────
            if (Matches(msg, "help", "what can you do", "what can you help",
                              "menu", "options", "guide"))
                return "🆘 <strong>Here's what I can help you with:</strong>\n\n📝 <strong>Accounts</strong> — register, login, logout\n🚢 <strong>Boats</strong> — add, edit, view, status\n📅 <strong>Bookings</strong> — book, cancel, view bookings\n📋 <strong>TNPA</strong> — approvals, rejections, admin portal\n🚗 <strong>Drivers</strong> — login, trips, assigned boat\n🌤️ <strong>Weather</strong> — sailing conditions\n📍 <strong>Locations</strong> — available harbours\n💰 <strong>Pricing</strong> — how pricing works\n🔑 <strong>Demo accounts</strong> — test credentials\n📞 <strong>Contact</strong> — support details\n\nJust type your question naturally — I understand plain language! 😊";

            // ── Default fallback ─────────────────────────────────────
            return BuildFallback(input);
        }

        private static string BuildFallback(string input)
        {
            return $"🤔 I'm not sure I understood <em>\"{input}\"</em>.\n\nHere are some things I can help with:\n\n• <strong>Register or Login</strong> — type \"how do I register\" or \"how do I login\"\n• <strong>Booking a boat</strong> — type \"how do I book a boat\"\n• <strong>TNPA approvals</strong> — type \"how does TNPA approval work\"\n• <strong>Driver portal</strong> — type \"driver login\"\n• <strong>Weather</strong> — type \"tell me about the weather feature\"\n• <strong>Help</strong> — type \"help\" for a full menu\n\nOr type your question differently and I'll try again! 😊";
        }

        private static bool Matches(string input, params string[] keywords)
        {
            return keywords.Any(k => input.Contains(k));
        }

        private static string GetUserName(System.Web.HttpSessionStateBase session)
        {
            return (session["BoatOwnerName"]
                 ?? session["CustomerName"]
                 ?? session["TnpaAdminName"]
                 ?? session["DriverName"]) as string;
        }
    }
}
