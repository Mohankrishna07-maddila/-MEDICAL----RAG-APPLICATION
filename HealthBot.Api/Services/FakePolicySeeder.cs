namespace HealthBot.Api.Services;

using System.Collections.Generic;
using HealthBot.Api.Models;




public class FakePolicySeeder
{
    public List<(string Text, Dictionary<string, string> Metadata)> GeneratePolicies()
    {
        var policies = new List<(string, Dictionary<string, string>)>();

        // 1. GOLD PLAN (Customer)
        var goldPlan = """
        GOLD PLAN POLICY - v2 (2025)
        Coverage: Full hospitalization up to $500,000.
        Benefits:
        - Cashless treatment at 5000+ network hospitals.
        - Pre-hospitalization: Covered for 60 days.
        - Post-hospitalization: Covered for 90 days.
        - Maternity: Covered after 2 year waiting period (Limit: $5000).
        - No copay for network hospitals.
        
        Exclusions:
        - Cosmetic surgery.
        - Experimental treatments.
        
        Claim Process:
        Use the mobile app or call 1-800-GOLD-HELP.
        """;
        
        policies.Add((goldPlan, new Dictionary<string, string>
        {
            { "policy_id", "POL_GOLD" },
            { "role", "customer" },
            { "plan_type", "gold" },
            { "version", "v2" },
            { "created_at", "2025-01-01" },
            { "source", "official_doc" },
            { "confidence", "1.0" }
        }));

        // 2. SILVER PLAN (Customer) - Different rules
        var silverPlan = """
        SILVER PLAN POLICY - v1 (2024)
        Coverage: Hospitalization up to $100,000.
        Benefits:
        - Cashless treatment at 2000+ network hospitals.
        - Pre-hospitalization: 30 days.
        - Post-hospitalization: 60 days.
        - Maternity: NOT Covered.
        - Copay: 10% on all claims.
        
        Claim Process:
        Submit physical forms within 15 days of discharge.
        """;
        policies.Add((silverPlan, new Dictionary<string, string>
        {
            { "policy_id", "POL_SILVER" },
            { "role", "customer" },
            { "plan_type", "silver" },
            { "version", "v1" },
            { "created_at", "2024-06-15" },
            { "source", "official_doc" },
            { "confidence", "0.95" }
        }));

        // 3. EMPLOYEE INTERNAL DOC (Confidential)
        var internalDoc = """
        INTERNAL SOP: CLAIM APPROVAL GUIDELINES
        Step 1: Verify policy status is 'Active'.
        Step 2: Check standard exclusions (cosmetic, alcohol-related).
        Step 3: If amount > $50k, request Manager Approval.
        Step 4: For VIP customers (Gold), prioritize processing within 2 hours.
        
        WARNING: Do not share these internal steps with customers.
        """;
        policies.Add((internalDoc, new Dictionary<string, string>
        {
            { "policy_id", "POL_EMP" },
            { "role", "employee" }, // Different role!
            { "sensitivity", "high" },
            { "version", "v3" },
            { "created_at", "2025-02-01" },
            { "source", "internal_sop" },
            { "confidence", "1.0" }
        }));

        return policies;
    }
}
