-- phpMyAdmin SQL Dump
-- version 5.2.3
-- https://www.phpmyadmin.net/
--
-- Host: localhost
-- Generation Time: Sep 17, 2026 at 03:55 PM
-- Server version: 11.4.13-MariaDB-cll-lve-log
-- PHP Version: 7.2.34

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `bckbbjxq_real`
--

-- --------------------------------------------------------

--
-- Table structure for table `activations`
--

CREATE TABLE `activations` (
  `id` int(10) UNSIGNED NOT NULL,
  `user_id` int(10) UNSIGNED NOT NULL,
  `code` varchar(191) NOT NULL,
  `completed` tinyint(1) NOT NULL DEFAULT 0,
  `completed_at` timestamp NULL DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `assets`
--

CREATE TABLE `assets` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `asset_type_id` int(11) DEFAULT NULL,
  `office_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `purchase_date` date DEFAULT NULL,
  `purchase_price` decimal(65,2) DEFAULT NULL,
  `value` decimal(65,2) DEFAULT NULL,
  `life_span` int(11) DEFAULT NULL,
  `salvage_value` decimal(65,2) DEFAULT NULL,
  `serial_number` text DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `files` text DEFAULT NULL,
  `purchase_year` text DEFAULT NULL,
  `status` enum('active','inactive','sold','damaged','written_off') DEFAULT NULL,
  `active` tinyint(4) DEFAULT 0,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `asset_depreciation`
--

CREATE TABLE `asset_depreciation` (
  `id` int(10) UNSIGNED NOT NULL,
  `asset_id` int(11) DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `beginning_value` decimal(65,2) DEFAULT NULL,
  `depreciation_value` decimal(65,2) DEFAULT NULL,
  `rate` decimal(65,2) DEFAULT NULL,
  `cost` decimal(65,2) DEFAULT NULL,
  `accumulated` decimal(65,2) DEFAULT NULL,
  `ending_value` decimal(65,2) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `asset_types`
--

CREATE TABLE `asset_types` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `gl_account_fixed_asset_id` int(11) DEFAULT NULL,
  `gl_account_asset_id` int(11) DEFAULT NULL,
  `gl_account_contra_asset_id` int(11) DEFAULT NULL,
  `gl_account_expense_id` int(11) DEFAULT NULL,
  `gl_account_liability_id` int(11) DEFAULT NULL,
  `gl_account_income_id` int(11) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `audit_trail`
--

CREATE TABLE `audit_trail` (
  `id` int(10) UNSIGNED NOT NULL,
  `user_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `office_id` int(11) DEFAULT NULL,
  `module` varchar(191) DEFAULT NULL,
  `action` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `charges`
--

CREATE TABLE `charges` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `currency_id` int(11) DEFAULT NULL,
  `product` enum('loan','savings','shares','client') NOT NULL,
  `charge_type` enum('disbursement','disbursement_repayment','specified_due_date','installment_fee','overdue_installment_fee','loan_rescheduling_fee','overdue_maturity','savings_activation','withdrawal_fee','annual_fee','monthly_fee','activation','shares_purchase','shares_redeem') NOT NULL,
  `charge_option` enum('flat','percentage','installment_principal_due','installment_principal_interest_due','installment_interest_due','installment_total_due','total_due','principal_due','interest_due','total_outstanding','original_principal') NOT NULL,
  `charge_frequency` tinyint(4) NOT NULL DEFAULT 0,
  `charge_frequency_type` enum('days','weeks','months','years') NOT NULL DEFAULT 'days',
  `charge_frequency_amount` int(11) NOT NULL DEFAULT 0,
  `amount` decimal(65,2) DEFAULT NULL,
  `minimum_amount` decimal(65,2) DEFAULT NULL,
  `maximum_amount` decimal(65,2) DEFAULT NULL,
  `charge_payment_mode` enum('regular','account_transfer') NOT NULL DEFAULT 'regular',
  `active` tinyint(4) NOT NULL DEFAULT 1,
  `penalty` tinyint(4) NOT NULL DEFAULT 0,
  `override` tinyint(4) NOT NULL DEFAULT 0,
  `gl_account_income_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `clients`
--

CREATE TABLE `clients` (
  `id` int(250) UNSIGNED NOT NULL,
  `client_id` int(255) DEFAULT NULL,
  `bvn` varchar(255) DEFAULT NULL,
  `country_id` int(11) DEFAULT NULL,
  `office_id` int(11) DEFAULT NULL,
  `user_id` int(11) DEFAULT NULL,
  `staff_id` int(11) DEFAULT NULL,
  `referred_by_id` int(11) DEFAULT NULL,
  `account_no` varchar(191) DEFAULT NULL,
  `old_account_no` varchar(191) DEFAULT NULL,
  `external_id` varchar(191) DEFAULT NULL,
  `title` varchar(191) DEFAULT NULL,
  `first_name` varchar(191) DEFAULT NULL,
  `middle_name` varchar(191) DEFAULT NULL,
  `last_name` varchar(191) DEFAULT NULL,
  `full_name` varchar(191) DEFAULT NULL,
  `incorporation_number` varchar(191) DEFAULT NULL,
  `display_name` varchar(191) DEFAULT NULL,
  `picture` varchar(191) DEFAULT NULL,
  `mobile` varchar(191) DEFAULT NULL,
  `phone` varchar(191) DEFAULT NULL,
  `email` varchar(191) DEFAULT NULL,
  `gender` enum('male','female','other','unspecified') DEFAULT NULL,
  `client_type` enum('individual','business','ngo','other') DEFAULT NULL,
  `status` enum('pending','active','inactive','declined','closed') NOT NULL DEFAULT 'pending',
  `marital_status` enum('married','single','divorced','widowed','unspecified') DEFAULT NULL,
  `dob` date DEFAULT NULL,
  `street` varchar(191) DEFAULT NULL,
  `ward` varchar(191) DEFAULT NULL,
  `district` varchar(191) DEFAULT NULL,
  `region` varchar(191) DEFAULT NULL,
  `address` text DEFAULT NULL,
  `joined_date` date DEFAULT NULL,
  `activated_date` date DEFAULT NULL,
  `reactivated_date` date DEFAULT NULL,
  `declined_date` date DEFAULT NULL,
  `declined_reason` text DEFAULT NULL,
  `closed_reason` text DEFAULT NULL,
  `closed_date` date DEFAULT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `inactive_reason` text DEFAULT NULL,
  `inactive_date` date DEFAULT NULL,
  `inactive_by_id` int(11) DEFAULT NULL,
  `activated_by_id` int(11) DEFAULT NULL,
  `reactivated_by_id` int(11) DEFAULT NULL,
  `declined_by_id` int(11) DEFAULT NULL,
  `closed_by_id` int(11) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `occupation` varchar(191) DEFAULT NULL,
  `postal_code` varchar(191) DEFAULT NULL,
  `country` varchar(191) DEFAULT NULL,
  `state` varchar(191) DEFAULT NULL,
  `city` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL,
  `deleted_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `client_identifications`
--

CREATE TABLE `client_identifications` (
  `id` int(10) UNSIGNED NOT NULL,
  `client_id` int(11) DEFAULT NULL,
  `client_identification_type_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `active` tinyint(4) NOT NULL DEFAULT 1,
  `notes` text DEFAULT NULL,
  `attachment` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `client_identification_types`
--

CREATE TABLE `client_identification_types` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `client_next_of_gaur`
--

CREATE TABLE `client_next_of_gaur` (
  `id` int(10) UNSIGNED NOT NULL,
  `client_id` int(11) DEFAULT NULL,
  `client_relationship_id` int(11) DEFAULT NULL,
  `qualification` varchar(191) DEFAULT NULL,
  `first_name` varchar(191) DEFAULT NULL,
  `middle_name` varchar(191) DEFAULT NULL,
  `last_name` varchar(191) DEFAULT NULL,
  `ward` varchar(191) DEFAULT NULL,
  `street` varchar(191) DEFAULT NULL,
  `district` varchar(191) DEFAULT NULL,
  `region` varchar(191) DEFAULT NULL,
  `address` text DEFAULT NULL,
  `picture` varchar(191) DEFAULT NULL,
  `mobile` varchar(191) DEFAULT NULL,
  `phone` varchar(191) DEFAULT NULL,
  `email` varchar(191) DEFAULT NULL,
  `gender` enum('male','female','other','unspecified') DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `client_next_of_kin`
--

CREATE TABLE `client_next_of_kin` (
  `id` int(10) UNSIGNED NOT NULL,
  `client_id` int(11) DEFAULT NULL,
  `client_relationship_id` int(11) DEFAULT NULL,
  `qualification` varchar(191) DEFAULT NULL,
  `first_name` varchar(191) DEFAULT NULL,
  `middle_name` varchar(191) DEFAULT NULL,
  `last_name` varchar(191) DEFAULT NULL,
  `ward` varchar(191) DEFAULT NULL,
  `street` varchar(191) DEFAULT NULL,
  `district` varchar(191) DEFAULT NULL,
  `region` varchar(191) DEFAULT NULL,
  `address` text DEFAULT NULL,
  `picture` varchar(191) DEFAULT NULL,
  `mobile` varchar(191) DEFAULT NULL,
  `phone` varchar(191) DEFAULT NULL,
  `email` varchar(191) DEFAULT NULL,
  `gender` enum('male','female','other','unspecified') DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `client_profession`
--

CREATE TABLE `client_profession` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `client_relationships`
--

CREATE TABLE `client_relationships` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `client_users`
--

CREATE TABLE `client_users` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `client_id` int(11) DEFAULT NULL,
  `user_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `collateral`
--

CREATE TABLE `collateral` (
  `id` int(10) UNSIGNED NOT NULL,
  `loan_id` int(11) DEFAULT NULL,
  `client_id` int(11) DEFAULT NULL,
  `collateral_type_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `serial` varchar(191) DEFAULT NULL,
  `value` decimal(65,4) DEFAULT NULL,
  `description` text DEFAULT NULL,
  `picture` text DEFAULT NULL,
  `gallery` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `collateral_types`
--

CREATE TABLE `collateral_types` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `communication_campaigns`
--

CREATE TABLE `communication_campaigns` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `type` enum('sms','email') DEFAULT NULL,
  `name` text DEFAULT NULL,
  `description` text DEFAULT NULL,
  `report_start_date` date DEFAULT NULL,
  `report_start_time` varchar(191) DEFAULT NULL,
  `recurrence_type` enum('none','schedule') DEFAULT NULL,
  `recur_frequency` enum('days','months','weeks','years') DEFAULT NULL,
  `recur_interval` varchar(191) DEFAULT NULL,
  `email_recipients` text DEFAULT NULL,
  `email_subject` varchar(191) DEFAULT NULL,
  `message` text DEFAULT NULL,
  `email_attachment_file_format` enum('pdf','csv','xls') DEFAULT NULL,
  `recipients_category` enum('all_clients','active_clients','prospective_clients','active_loans','loans_in_arrears','overdue_loans','happy_birthday') DEFAULT NULL,
  `report_attachment` enum('loan_schedule','loan_statement','savings_statement','audit_report','group_indicator_report') DEFAULT NULL,
  `from_day` varchar(191) DEFAULT NULL,
  `to_day` varchar(191) DEFAULT NULL,
  `office_id` varchar(191) DEFAULT NULL,
  `loan_officer_id` varchar(191) DEFAULT NULL,
  `gl_account_id` varchar(191) DEFAULT NULL,
  `manual_entries` varchar(191) DEFAULT NULL,
  `loan_status` varchar(191) DEFAULT NULL,
  `loan_product_id` varchar(191) DEFAULT NULL,
  `last_run_date` date DEFAULT NULL,
  `next_run_date` date DEFAULT NULL,
  `last_run_time` date DEFAULT NULL,
  `next_run_time` date DEFAULT NULL,
  `number_of_runs` int(11) NOT NULL DEFAULT 0,
  `number_of_recipients` int(11) NOT NULL DEFAULT 0,
  `active` tinyint(4) NOT NULL DEFAULT 1,
  `sent` tinyint(4) NOT NULL DEFAULT 0,
  `status` enum('pending','active','declined','inactive') NOT NULL DEFAULT 'pending',
  `approved_by_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `countries`
--

CREATE TABLE `countries` (
  `id` int(10) UNSIGNED NOT NULL,
  `sortname` varchar(191) NOT NULL,
  `name` varchar(191) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `currencies`
--

CREATE TABLE `currencies` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `code` varchar(191) DEFAULT NULL,
  `symbol` varchar(191) DEFAULT NULL,
  `decimals` varchar(191) DEFAULT '2',
  `xrate` decimal(65,8) DEFAULT NULL,
  `international_code` varchar(191) DEFAULT NULL,
  `active` tinyint(4) NOT NULL DEFAULT 1,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `custom_fields`
--

CREATE TABLE `custom_fields` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `category` varchar(191) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `field_type` enum('number','textfield','date','decimal','textarea','checkbox','radiobox','select') NOT NULL DEFAULT 'textfield',
  `required` tinyint(4) NOT NULL DEFAULT 0,
  `radio_box_values` text DEFAULT NULL,
  `checkbox_values` text DEFAULT NULL,
  `select_values` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `custom_fields_meta`
--

CREATE TABLE `custom_fields_meta` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `category` varchar(191) DEFAULT NULL,
  `parent_id` int(11) DEFAULT NULL,
  `custom_field_id` int(11) DEFAULT NULL,
  `name` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `documents`
--

CREATE TABLE `documents` (
  `id` int(10) UNSIGNED NOT NULL,
  `type` enum('client','loan','group','savings','identification','shares','repayment') DEFAULT NULL,
  `record_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `size` varchar(191) DEFAULT NULL,
  `location` text DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `expenses`
--

CREATE TABLE `expenses` (
  `id` int(10) UNSIGNED NOT NULL,
  `office_id` int(10) UNSIGNED DEFAULT NULL,
  `created_by_id` int(10) UNSIGNED DEFAULT NULL,
  `expense_type_id` int(10) UNSIGNED DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `amount` decimal(65,2) NOT NULL DEFAULT 0.00,
  `date` date DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `recurring` tinyint(4) NOT NULL DEFAULT 0,
  `recur_frequency` varchar(191) NOT NULL DEFAULT '31',
  `recur_start_date` date DEFAULT NULL,
  `recur_end_date` date DEFAULT NULL,
  `recur_next_date` date DEFAULT NULL,
  `recur_type` enum('day','week','month','year') NOT NULL DEFAULT 'month',
  `status` enum('pending','approved','declined') NOT NULL DEFAULT 'approved',
  `approved_date` date DEFAULT NULL,
  `approved_by_id` int(10) UNSIGNED DEFAULT NULL,
  `declined_date` date DEFAULT NULL,
  `declined_by_id` int(10) UNSIGNED DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `files` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `expense_budgets`
--

CREATE TABLE `expense_budgets` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(10) UNSIGNED DEFAULT NULL,
  `office_id` int(10) UNSIGNED DEFAULT NULL,
  `expense_type_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `date` date DEFAULT NULL,
  `amount` decimal(65,2) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `status` enum('pending','approved','declined') NOT NULL DEFAULT 'approved',
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `expense_types`
--

CREATE TABLE `expense_types` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `gl_account_asset_id` int(11) DEFAULT NULL,
  `gl_account_expense_id` int(11) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `funds`
--

CREATE TABLE `funds` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `gl_accounts`
--

CREATE TABLE `gl_accounts` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `parent_id` int(11) DEFAULT NULL,
  `gl_code` varchar(191) DEFAULT NULL,
  `account_type` enum('asset','liability','equity','income','expense') NOT NULL,
  `active` tinyint(4) NOT NULL DEFAULT 1,
  `manual_entries` tinyint(4) NOT NULL DEFAULT 1,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `gl_closures`
--

CREATE TABLE `gl_closures` (
  `id` int(10) UNSIGNED NOT NULL,
  `office_id` int(11) DEFAULT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `closing_date` date NOT NULL,
  `modified_by_id` int(11) DEFAULT NULL,
  `gl_reference` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `gl_journal_entries`
--

CREATE TABLE `gl_journal_entries` (
  `id` int(10) UNSIGNED NOT NULL,
  `office_id` int(11) DEFAULT NULL,
  `gl_account_id` int(11) DEFAULT NULL,
  `currency_id` int(11) DEFAULT NULL,
  `transaction_type` enum('disbursement','accrual','deposit','withdrawal','manual_entry','pay_charge','transfer_fund','expense','payroll','income','fee','penalty','interest','dividend','guarantee','write_off','repayment','repayment_disbursement','repayment_recovery','interest_accrual','fee_accrual','savings','shares','asset','asset_income','asset_expense','asset_depreciation') DEFAULT 'repayment',
  `transaction_sub_type` enum('overpayment','repayment_interest','repayment_principal','repayment_fees','repayment_penalty') DEFAULT NULL,
  `debit` decimal(65,4) DEFAULT NULL,
  `credit` decimal(65,4) DEFAULT NULL,
  `reversed` tinyint(4) NOT NULL DEFAULT 0,
  `name` text DEFAULT NULL,
  `reference` varchar(191) DEFAULT NULL,
  `loan_id` int(11) DEFAULT NULL,
  `loan_transaction_id` int(11) DEFAULT NULL,
  `savings_transaction_id` int(11) DEFAULT NULL,
  `savings_id` int(11) DEFAULT NULL,
  `shares_transaction_id` int(11) DEFAULT NULL,
  `payroll_transaction_id` int(11) DEFAULT NULL,
  `payment_detail_id` int(11) DEFAULT NULL,
  `transaction_id` int(11) DEFAULT NULL,
  `gl_closure_id` int(11) DEFAULT NULL,
  `date` date DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `narration` longtext DEFAULT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `modified_by_id` int(11) DEFAULT NULL,
  `reconciled` tinyint(4) NOT NULL DEFAULT 0,
  `manual_entry` tinyint(4) NOT NULL DEFAULT 0,
  `approved` tinyint(4) NOT NULL DEFAULT 1,
  `approved_by_id` int(11) DEFAULT NULL,
  `approved_date` date DEFAULT NULL,
  `approved_notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `groups`
--

CREATE TABLE `groups` (
  `id` int(10) UNSIGNED NOT NULL,
  `old_group_id` varchar(191) DEFAULT NULL,
  `office_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `account_no` varchar(191) DEFAULT NULL,
  `external_id` varchar(191) DEFAULT NULL,
  `staff_id` int(11) DEFAULT NULL,
  `joined_date` date DEFAULT NULL,
  `activated_date` date DEFAULT NULL,
  `reactivated_date` date DEFAULT NULL,
  `declined_date` date DEFAULT NULL,
  `declined_reason` text DEFAULT NULL,
  `closed_reason` text DEFAULT NULL,
  `closed_date` date DEFAULT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `activated_by_id` int(11) DEFAULT NULL,
  `reactivated_by_id` int(11) DEFAULT NULL,
  `declined_by_id` int(11) DEFAULT NULL,
  `closed_by_id` int(11) DEFAULT NULL,
  `mobile` varchar(191) DEFAULT NULL,
  `phone` varchar(191) DEFAULT NULL,
  `email` varchar(191) DEFAULT NULL,
  `street` varchar(191) DEFAULT NULL,
  `ward` varchar(191) DEFAULT NULL,
  `district` varchar(191) DEFAULT NULL,
  `region` varchar(191) DEFAULT NULL,
  `address` text DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `status` enum('pending','active','inactive','declined','closed') NOT NULL DEFAULT 'pending',
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `group_clients`
--

CREATE TABLE `group_clients` (
  `id` int(10) UNSIGNED NOT NULL,
  `group_id` int(11) DEFAULT NULL,
  `client_id` int(11) DEFAULT NULL,
  `old_group_id` varchar(255) DEFAULT NULL,
  `old_client_id` varchar(255) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `group_loan_allocation`
--

CREATE TABLE `group_loan_allocation` (
  `id` int(10) UNSIGNED NOT NULL,
  `loan_id` int(11) DEFAULT NULL,
  `group_id` int(11) DEFAULT NULL,
  `client_id` int(11) DEFAULT NULL,
  `amount` decimal(65,4) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `group_users`
--

CREATE TABLE `group_users` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `group_id` int(11) DEFAULT NULL,
  `user_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `guarantors`
--

CREATE TABLE `guarantors` (
  `id` int(10) UNSIGNED NOT NULL,
  `country_id` int(11) DEFAULT NULL,
  `client_id` int(11) DEFAULT NULL,
  `savings_id` int(11) DEFAULT NULL,
  `loan_id` int(11) DEFAULT NULL,
  `loan_application_id` int(11) DEFAULT NULL,
  `is_client` tinyint(4) NOT NULL DEFAULT 0,
  `client_relationship_id` int(11) DEFAULT NULL,
  `amount` decimal(65,4) DEFAULT NULL,
  `title` varchar(191) DEFAULT NULL,
  `first_name` varchar(191) DEFAULT NULL,
  `middle_name` varchar(191) DEFAULT NULL,
  `last_name` varchar(191) DEFAULT NULL,
  `gender` enum('male','female','other','unspecified') DEFAULT NULL,
  `dob` date DEFAULT NULL,
  `street` varchar(191) DEFAULT NULL,
  `address` text DEFAULT NULL,
  `mobile` varchar(191) DEFAULT NULL,
  `phone` varchar(191) DEFAULT NULL,
  `email` varchar(191) DEFAULT NULL,
  `picture` text DEFAULT NULL,
  `work` varchar(191) DEFAULT NULL,
  `work_address` text DEFAULT NULL,
  `lock_funds` tinyint(4) NOT NULL DEFAULT 0,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `loans`
--

CREATE TABLE `loans` (
  `id` int(10) UNSIGNED NOT NULL,
  `client_type` enum('client','group') NOT NULL DEFAULT 'client',
  `loan_product_id` int(11) DEFAULT NULL,
  `client_id` int(11) DEFAULT NULL,
  `old_client_id` text DEFAULT NULL,
  `office_id` int(11) DEFAULT NULL,
  `group_id` int(11) DEFAULT NULL,
  `fund_id` int(11) DEFAULT NULL,
  `loan_purpose_id` int(11) DEFAULT NULL,
  `currency_id` int(11) DEFAULT NULL,
  `decimals` int(11) NOT NULL DEFAULT 2,
  `account_number` varchar(191) DEFAULT NULL,
  `external_id` varchar(191) DEFAULT NULL,
  `loan_officer_id` int(11) DEFAULT NULL,
  `principal` decimal(65,4) DEFAULT NULL,
  `applied_amount` decimal(65,4) DEFAULT NULL,
  `approved_amount` decimal(65,4) DEFAULT NULL,
  `principal_derived` decimal(65,4) DEFAULT NULL,
  `interest_derived` decimal(65,4) DEFAULT NULL,
  `fees_derived` decimal(65,4) DEFAULT NULL,
  `penalty_derived` decimal(65,4) DEFAULT NULL,
  `disbursement_fees` decimal(65,4) DEFAULT NULL,
  `processing_fee` decimal(65,4) DEFAULT NULL,
  `loan_term` int(11) DEFAULT NULL,
  `loan_term_type` enum('days','weeks','months','years') DEFAULT NULL,
  `repayment_frequency` int(11) DEFAULT NULL,
  `repayment_frequency_type` enum('days','weeks','months','years') DEFAULT NULL,
  `override_interest` tinyint(4) DEFAULT 0,
  `interest_rate` decimal(65,4) DEFAULT NULL,
  `override_interest_rate` decimal(65,4) DEFAULT NULL,
  `interest_rate_type` enum('day','week','month','year') DEFAULT NULL,
  `expected_disbursement_date` date DEFAULT NULL,
  `disbursement_date` date DEFAULT NULL,
  `expected_maturity_date` date DEFAULT NULL,
  `expected_first_repayment_date` date DEFAULT NULL,
  `repayments_number` int(11) DEFAULT NULL,
  `first_repayment_date` date DEFAULT NULL,
  `interest_method` enum('flat','declining_balance') DEFAULT NULL,
  `armotization_method` enum('equal_installment','equal_principal') DEFAULT NULL,
  `grace_on_interest_charged` int(11) DEFAULT NULL,
  `grace_on_principal` int(11) DEFAULT NULL,
  `grace_on_interest_payment` int(11) DEFAULT NULL,
  `status` enum('new','pending','approved','need_changes','disbursed','declined','rejected','withdrawn','written_off','closed','pending_reschedule','rescheduled','paid') NOT NULL DEFAULT 'pending',
  `created_by_id` int(11) DEFAULT NULL,
  `modified_by_id` int(11) DEFAULT NULL,
  `approved_by_id` int(11) DEFAULT NULL,
  `need_changes_by_id` int(11) DEFAULT NULL,
  `withdrawn_by_id` int(11) DEFAULT NULL,
  `declined_by_id` int(11) DEFAULT NULL,
  `written_off_by_id` int(11) DEFAULT NULL,
  `disbursed_by_id` int(11) DEFAULT NULL,
  `rescheduled_by_id` int(11) DEFAULT NULL,
  `closed_by_id` int(11) DEFAULT NULL,
  `created_date` date DEFAULT NULL,
  `modified_date` date DEFAULT NULL,
  `approved_date` date DEFAULT NULL,
  `need_changes_date` date DEFAULT NULL,
  `withdrawn_date` date DEFAULT NULL,
  `declined_date` date DEFAULT NULL,
  `written_off_date` date DEFAULT NULL,
  `rescheduled_date` date DEFAULT NULL,
  `closed_date` date DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `approved_notes` text DEFAULT NULL,
  `declined_notes` text DEFAULT NULL,
  `written_off_notes` text DEFAULT NULL,
  `disbursed_notes` text DEFAULT NULL,
  `withdrawn_notes` text DEFAULT NULL,
  `rescheduled_notes` text DEFAULT NULL,
  `closed_notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL,
  `deleted_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ROW_FORMAT=COMPRESSED;

-- --------------------------------------------------------

--
-- Table structure for table `loan_applications`
--

CREATE TABLE `loan_applications` (
  `id` int(10) UNSIGNED NOT NULL,
  `client_type` enum('client','group') NOT NULL DEFAULT 'client',
  `user_id` int(10) UNSIGNED DEFAULT NULL,
  `loan_id` int(10) UNSIGNED DEFAULT NULL,
  `loan_purpose_id` int(11) DEFAULT NULL,
  `currency_id` int(11) DEFAULT NULL,
  `office_id` int(10) UNSIGNED DEFAULT NULL,
  `client_id` int(10) UNSIGNED DEFAULT NULL,
  `group_id` int(10) UNSIGNED DEFAULT NULL,
  `loan_product_id` int(11) NOT NULL,
  `amount` decimal(65,4) NOT NULL DEFAULT 0.0000,
  `status` enum('approved','pending','declined') NOT NULL DEFAULT 'pending',
  `guarantor_ids` text DEFAULT NULL,
  `loan_term` int(11) DEFAULT NULL,
  `loan_term_type` enum('days','weeks','months','years') DEFAULT NULL,
  `approved_by_id` int(11) DEFAULT NULL,
  `declined_by_id` int(11) DEFAULT NULL,
  `approved_notes` text DEFAULT NULL,
  `declined_notes` text DEFAULT NULL,
  `declined_date` date DEFAULT NULL,
  `approved_date` date DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `loan_charges`
--

CREATE TABLE `loan_charges` (
  `id` int(10) UNSIGNED NOT NULL,
  `loan_id` int(11) DEFAULT NULL,
  `charge_id` int(11) DEFAULT NULL,
  `penalty` tinyint(4) NOT NULL DEFAULT 0,
  `waived` tinyint(4) NOT NULL DEFAULT 0,
  `charge_type` enum('disbursement','disbursement_repayment','specified_due_date','installment_fee','overdue_installment_fee','loan_rescheduling_fee','overdue_maturity') NOT NULL,
  `charge_option` enum('flat','percentage','installment_principal_due','installment_principal_interest_due','installment_interest_due','installment_total_due','total_due','original_principal') NOT NULL,
  `amount` decimal(65,2) DEFAULT NULL,
  `amount_paid` decimal(65,2) DEFAULT NULL,
  `due_date` date DEFAULT NULL,
  `grace_period` int(11) NOT NULL DEFAULT 0,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `loan_products`
--

CREATE TABLE `loan_products` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `short_name` varchar(191) DEFAULT NULL,
  `description` text DEFAULT NULL,
  `fund_id` int(11) DEFAULT NULL,
  `currency_id` int(11) DEFAULT NULL,
  `decimals` int(11) NOT NULL DEFAULT 2,
  `minimum_principal` decimal(65,4) DEFAULT NULL,
  `default_principal` decimal(65,4) DEFAULT NULL,
  `maximum_principal` decimal(65,4) DEFAULT NULL,
  `minimum_loan_term` int(11) DEFAULT NULL,
  `default_loan_term` int(11) DEFAULT NULL,
  `maximum_loan_term` int(11) DEFAULT NULL,
  `repayment_frequency` int(11) DEFAULT NULL,
  `repayment_frequency_type` enum('days','weeks','months','years') DEFAULT NULL,
  `minimum_interest_rate` decimal(65,4) DEFAULT NULL,
  `default_interest_rate` decimal(65,4) DEFAULT NULL,
  `maximum_interest_rate` decimal(65,4) DEFAULT NULL,
  `interest_rate_type` enum('day','week','month','year') DEFAULT NULL,
  `grace_on_interest_charged` int(11) DEFAULT NULL,
  `grace_on_principal` int(11) DEFAULT NULL,
  `grace_on_interest_payment` int(11) DEFAULT NULL,
  `allow_custom_grace` tinyint(4) NOT NULL DEFAULT 0,
  `allow_standing_instuctions` tinyint(4) NOT NULL DEFAULT 0,
  `interest_method` enum('flat','declining_balance') DEFAULT NULL,
  `armotization_method` enum('equal_installment','equal_principal') DEFAULT NULL,
  `interest_calculation_period_type` enum('daily','same') NOT NULL DEFAULT 'same',
  `year_days` enum('actual','360','364','365') NOT NULL DEFAULT '365',
  `month_days` enum('actual','30','31') NOT NULL DEFAULT '30',
  `loan_transaction_strategy` enum('penalty_fees_interest_principal','principal_interest_penalty_fees','interest_principal_penalty_fees') NOT NULL DEFAULT 'interest_principal_penalty_fees',
  `include_in_cycle` tinyint(4) NOT NULL DEFAULT 0,
  `lock_guarantee` tinyint(4) NOT NULL DEFAULT 0,
  `allocate_overpayments` tinyint(4) NOT NULL DEFAULT 0,
  `allow_additional_charges` tinyint(4) NOT NULL DEFAULT 0,
  `accounting_rule` enum('none','cash','accrual_periodic','accrual_upfront') NOT NULL DEFAULT 'cash',
  `npa_days` int(11) DEFAULT NULL,
  `arrears_grace_days` int(11) DEFAULT NULL,
  `npa_suspend_income` tinyint(4) NOT NULL DEFAULT 0,
  `gl_account_fund_source_id` int(11) DEFAULT NULL,
  `gl_account_loan_portfolio_id` int(11) DEFAULT NULL,
  `gl_account_receivable_interest_id` int(11) DEFAULT NULL,
  `gl_account_receivable_fee_id` int(11) DEFAULT NULL,
  `gl_account_receivable_penalty_id` int(11) DEFAULT NULL,
  `gl_account_loan_over_payments_id` int(11) DEFAULT NULL,
  `gl_account_suspended_income_id` int(11) DEFAULT NULL,
  `gl_account_income_interest_id` int(11) DEFAULT NULL,
  `gl_account_income_fee_id` int(11) DEFAULT NULL,
  `gl_account_income_penalty_id` int(11) DEFAULT NULL,
  `gl_account_income_recovery_id` int(11) DEFAULT NULL,
  `gl_account_loans_written_off_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `loan_product_charges`
--

CREATE TABLE `loan_product_charges` (
  `id` int(10) UNSIGNED NOT NULL,
  `loan_product_id` int(11) DEFAULT NULL,
  `charge_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `loan_provisioning_criteria`
--

CREATE TABLE `loan_provisioning_criteria` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `name` text DEFAULT NULL,
  `min` int(11) DEFAULT NULL,
  `max` int(11) DEFAULT NULL,
  `percentage` int(11) DEFAULT NULL,
  `gl_account_liability_id` int(11) DEFAULT NULL,
  `gl_account_expense_id` int(11) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `active` tinyint(4) NOT NULL DEFAULT 1,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `loan_purposes`
--

CREATE TABLE `loan_purposes` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `loan_repayment_schedules`
--

CREATE TABLE `loan_repayment_schedules` (
  `id` int(10) UNSIGNED NOT NULL,
  `loan_id` int(11) DEFAULT NULL,
  `installment` int(11) DEFAULT NULL,
  `due_date` date DEFAULT NULL,
  `from_date` date DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `principal` decimal(65,4) DEFAULT NULL,
  `principal_waived` decimal(65,4) DEFAULT NULL,
  `principal_written_off` decimal(65,4) DEFAULT NULL,
  `principal_paid` decimal(65,4) DEFAULT NULL,
  `interest` decimal(65,4) DEFAULT NULL,
  `interest_waived` decimal(65,4) DEFAULT NULL,
  `interest_written_off` decimal(65,4) DEFAULT NULL,
  `interest_paid` decimal(65,4) DEFAULT NULL,
  `fees` decimal(65,4) DEFAULT NULL,
  `fees_waived` decimal(65,4) DEFAULT NULL,
  `fees_written_off` decimal(65,4) DEFAULT NULL,
  `fees_paid` decimal(65,4) DEFAULT NULL,
  `penalty` decimal(65,4) DEFAULT NULL,
  `penalty_waived` decimal(65,4) DEFAULT NULL,
  `penalty_written_off` decimal(65,4) DEFAULT NULL,
  `penalty_paid` decimal(65,4) DEFAULT NULL,
  `total_due` decimal(65,4) DEFAULT NULL,
  `total_paid_advance` decimal(65,4) DEFAULT NULL,
  `total_paid_late` decimal(65,4) DEFAULT NULL,
  `paid` tinyint(4) NOT NULL DEFAULT 0,
  `modified_by_id` int(11) DEFAULT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ROW_FORMAT=COMPRESSED;

-- --------------------------------------------------------

--
-- Table structure for table `loan_reschedule_requests`
--

CREATE TABLE `loan_reschedule_requests` (
  `id` int(10) UNSIGNED NOT NULL,
  `loan_id` int(11) DEFAULT NULL,
  `principal` decimal(65,4) DEFAULT NULL,
  `status` enum('pending','approved','rejected') NOT NULL DEFAULT 'pending',
  `created_by_id` int(11) DEFAULT NULL,
  `modified_by_id` int(11) DEFAULT NULL,
  `approved_by_id` int(11) DEFAULT NULL,
  `rejected_by_id` int(11) DEFAULT NULL,
  `created_date` date DEFAULT NULL,
  `modified_date` date DEFAULT NULL,
  `approved_date` date DEFAULT NULL,
  `rejected_date` date DEFAULT NULL,
  `reschedule_from_date` date DEFAULT NULL,
  `recalculate_interest` int(11) NOT NULL DEFAULT 0,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `loan_transactions`
--

CREATE TABLE `loan_transactions` (
  `id` int(10) UNSIGNED NOT NULL,
  `loan_id` int(11) DEFAULT NULL,
  `office_id` int(11) DEFAULT NULL,
  `client_id` int(11) DEFAULT NULL,
  `payment_type_id` int(11) DEFAULT NULL,
  `transaction_type` enum('repayment','repayment_disbursement','write_off','write_off_recovery','disbursement','interest_accrual','fee_accrual','penalty_accrual','deposit','withdrawal','manual_entry','pay_charge','transfer_fund','interest','income','fee','disbursement_fee','installment_fee','specified_due_date_fee','overdue_maturity','overdue_installment_fee','loan_rescheduling_fee','penalty','interest_waiver','charge_waiver') DEFAULT 'repayment',
  `created_by_id` int(11) DEFAULT NULL,
  `modified_by_id` int(11) DEFAULT NULL,
  `payment_detail_id` int(11) DEFAULT NULL,
  `charge_id` int(11) DEFAULT NULL,
  `loan_repayment_schedule_id` int(11) DEFAULT NULL,
  `debit` decimal(65,4) DEFAULT NULL,
  `credit` decimal(65,4) DEFAULT NULL,
  `balance` decimal(65,4) DEFAULT NULL,
  `amount` decimal(65,4) DEFAULT NULL,
  `reversible` tinyint(4) NOT NULL DEFAULT 0,
  `reversed` tinyint(4) NOT NULL DEFAULT 0,
  `reversal_type` enum('system','user','none') NOT NULL DEFAULT 'none',
  `payment_apply_to` enum('interest','principal','fees','penalty','regular') DEFAULT 'regular',
  `status` enum('pending','approved','declined') DEFAULT 'pending',
  `approved_by_id` int(11) DEFAULT NULL,
  `approved_date` date DEFAULT NULL,
  `interest` decimal(65,4) DEFAULT NULL,
  `principal` decimal(65,4) DEFAULT NULL,
  `fee` decimal(65,4) DEFAULT NULL,
  `penalty` decimal(65,4) DEFAULT NULL,
  `overpayment` decimal(65,4) DEFAULT NULL,
  `date` date DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `receipt` text DEFAULT NULL,
  `principal_derived` decimal(65,4) DEFAULT NULL,
  `interest_derived` decimal(65,4) DEFAULT NULL,
  `fees_derived` decimal(65,4) DEFAULT NULL,
  `penalty_derived` decimal(65,4) DEFAULT NULL,
  `overpayment_derived` decimal(65,4) DEFAULT NULL,
  `unrecognized_income_derived` decimal(65,4) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL,
  `deleted_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ROW_FORMAT=COMPRESSED;

-- --------------------------------------------------------

--
-- Table structure for table `loan_transaction_repayment_schedule_mappings`
--

CREATE TABLE `loan_transaction_repayment_schedule_mappings` (
  `id` int(10) UNSIGNED NOT NULL,
  `loan_repayment_schedule_id` int(11) DEFAULT NULL,
  `loan_transaction_id` int(11) DEFAULT NULL,
  `interest` decimal(65,4) DEFAULT NULL,
  `principal` decimal(65,4) DEFAULT NULL,
  `fee` decimal(65,4) DEFAULT NULL,
  `penalty` decimal(65,4) DEFAULT NULL,
  `overpayment` decimal(65,4) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `migrations`
--

CREATE TABLE `migrations` (
  `id` int(10) UNSIGNED NOT NULL,
  `migration` varchar(191) NOT NULL,
  `batch` int(11) NOT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `notes`
--

CREATE TABLE `notes` (
  `id` int(10) UNSIGNED NOT NULL,
  `reference_id` int(11) DEFAULT NULL,
  `type` enum('client','loan','group','savings','identification','shares','repayment') DEFAULT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `modified_by_id` int(11) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `offices`
--

CREATE TABLE `offices` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `parent_id` int(11) DEFAULT NULL,
  `external_id` varchar(191) DEFAULT NULL,
  `opening_date` date DEFAULT NULL,
  `address` text DEFAULT NULL,
  `phone` text DEFAULT NULL,
  `email` text DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `manager_id` int(11) DEFAULT NULL,
  `active` tinyint(4) DEFAULT 1,
  `default_office` tinyint(4) DEFAULT 0,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL,
  `deleted_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `office_transactions`
--

CREATE TABLE `office_transactions` (
  `id` int(10) UNSIGNED NOT NULL,
  `from_office_id` int(11) DEFAULT NULL,
  `to_office_id` int(11) DEFAULT NULL,
  `currency_id` int(11) DEFAULT NULL,
  `amount` decimal(65,8) DEFAULT NULL,
  `date` date DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `other_income`
--

CREATE TABLE `other_income` (
  `id` int(10) UNSIGNED NOT NULL,
  `office_id` int(10) UNSIGNED DEFAULT NULL,
  `created_by_id` int(10) UNSIGNED DEFAULT NULL,
  `other_income_type_id` int(10) UNSIGNED DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `amount` decimal(65,2) NOT NULL DEFAULT 0.00,
  `date` date DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `files` text DEFAULT NULL,
  `status` enum('pending','approved','declined') NOT NULL DEFAULT 'approved',
  `approved_date` date DEFAULT NULL,
  `approved_by_id` int(10) UNSIGNED DEFAULT NULL,
  `declined_date` date DEFAULT NULL,
  `declined_by_id` int(10) UNSIGNED DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `other_income_types`
--

CREATE TABLE `other_income_types` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `gl_account_asset_id` int(11) DEFAULT NULL,
  `gl_account_income_id` int(11) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `payment_details`
--

CREATE TABLE `payment_details` (
  `id` int(10) UNSIGNED NOT NULL,
  `payment_type_id` int(11) DEFAULT NULL,
  `account_number` varchar(191) DEFAULT NULL,
  `cheque_number` varchar(191) DEFAULT NULL,
  `routing_code` varchar(191) DEFAULT NULL,
  `receipt_number` varchar(191) DEFAULT NULL,
  `bank` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `payment_types`
--

CREATE TABLE `payment_types` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `is_cash` tinyint(4) NOT NULL DEFAULT 0,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `payment_type_details`
--

CREATE TABLE `payment_type_details` (
  `id` int(10) UNSIGNED NOT NULL,
  `type` enum('loan','savings','share','client','journal') DEFAULT NULL,
  `reference_id` int(11) NOT NULL,
  `account_number` varchar(191) DEFAULT NULL,
  `cheque_number` varchar(191) DEFAULT NULL,
  `routing_code` varchar(191) DEFAULT NULL,
  `receipt_number` varchar(191) DEFAULT NULL,
  `bank` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `payroll`
--

CREATE TABLE `payroll` (
  `id` int(10) UNSIGNED NOT NULL,
  `payroll_template_id` int(10) UNSIGNED DEFAULT NULL,
  `gl_account_expense_id` int(10) UNSIGNED DEFAULT NULL,
  `gl_account_asset_id` int(10) UNSIGNED DEFAULT NULL,
  `user_id` int(10) UNSIGNED DEFAULT NULL,
  `office_id` int(10) UNSIGNED DEFAULT NULL,
  `employee_name` varchar(191) DEFAULT NULL,
  `business_name` varchar(191) DEFAULT NULL,
  `payment_method` varchar(191) DEFAULT NULL,
  `payment_type_id` varchar(191) DEFAULT NULL,
  `bank_name` varchar(191) DEFAULT NULL,
  `account_number` varchar(191) DEFAULT NULL,
  `description` varchar(191) DEFAULT NULL,
  `comments` text DEFAULT NULL,
  `paid_amount` decimal(10,2) NOT NULL DEFAULT 0.00,
  `date` date DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `recurring` tinyint(4) NOT NULL DEFAULT 0,
  `recur_frequency` varchar(191) NOT NULL DEFAULT '31',
  `recur_start_date` date DEFAULT NULL,
  `recur_end_date` date DEFAULT NULL,
  `recur_next_date` date DEFAULT NULL,
  `recur_type` enum('days','weeks','months','years') NOT NULL DEFAULT 'months',
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `payroll_meta`
--

CREATE TABLE `payroll_meta` (
  `id` int(10) UNSIGNED NOT NULL,
  `payroll_id` int(10) UNSIGNED NOT NULL,
  `payroll_template_meta_id` int(10) UNSIGNED DEFAULT NULL,
  `value` decimal(65,2) DEFAULT NULL,
  `is_tax` tinyint(4) DEFAULT 0,
  `is_percentage` tinyint(4) DEFAULT 0,
  `position` enum('top_left','top_right','bottom_left','bottom_right') DEFAULT 'bottom_left',
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `payroll_templates`
--

CREATE TABLE `payroll_templates` (
  `id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `picture` varchar(191) DEFAULT NULL,
  `active` tinyint(4) DEFAULT 1,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `payroll_template_meta`
--

CREATE TABLE `payroll_template_meta` (
  `id` int(10) UNSIGNED NOT NULL,
  `payroll_template_id` int(10) UNSIGNED NOT NULL,
  `name` varchar(191) DEFAULT NULL,
  `position` enum('top_left','top_right','bottom_left','bottom_right','none') DEFAULT 'bottom_left',
  `type` enum('addition','deduction') DEFAULT 'addition',
  `is_default` tinyint(4) NOT NULL DEFAULT 0,
  `is_tax` tinyint(4) NOT NULL DEFAULT 0,
  `is_percentage` tinyint(4) NOT NULL DEFAULT 0,
  `tax_on` enum('net','gross') DEFAULT 'net',
  `default_value` decimal(65,2) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `permissions`
--

CREATE TABLE `permissions` (
  `id` int(10) UNSIGNED NOT NULL,
  `parent_id` int(11) DEFAULT 0,
  `name` varchar(191) NOT NULL,
  `slug` varchar(191) DEFAULT NULL,
  `description` text DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `persistences`
--

CREATE TABLE `persistences` (
  `id` int(10) UNSIGNED NOT NULL,
  `user_id` int(10) UNSIGNED NOT NULL,
  `code` varchar(191) NOT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `reminders`
--

CREATE TABLE `reminders` (
  `id` int(10) UNSIGNED NOT NULL,
  `user_id` int(10) UNSIGNED NOT NULL,
  `code` varchar(191) NOT NULL,
  `completed` tinyint(1) NOT NULL DEFAULT 0,
  `completed_at` timestamp NULL DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `report_scheduler`
--

CREATE TABLE `report_scheduler` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `description` text DEFAULT NULL,
  `report_start_date` date DEFAULT NULL,
  `report_start_time` varchar(191) DEFAULT NULL,
  `recurrence_type` enum('none','schedule') DEFAULT NULL,
  `recur_frequency` enum('daily','monthly','weekly','yearly') DEFAULT NULL,
  `recur_interval` varchar(191) DEFAULT NULL,
  `email_recipients` text DEFAULT NULL,
  `email_subject` varchar(191) DEFAULT NULL,
  `email_message` text DEFAULT NULL,
  `email_attachment_file_format` enum('pdf','csv','xls') DEFAULT NULL,
  `report_category` enum('client_report','loan_report','financial_report','group_report','savings_report','organisation_report') DEFAULT NULL,
  `report_name` enum('disbursed_loans_report','loan_portfolio_report','expected_repayments_report','repayments_report','collection_report','arrears_report','balance_sheet','trial_balance','profit_and_loss','cash_flow','provisioning','historical_income_statement','journals_report','accrued_interest','client_numbers_report','clients_overview','top_clients_report','loan_sizes_report','group_report','group_breakdown','savings_account_report','savings_balance_report','savings_transaction_report','fixed_term_maturity_report','products_summary','individual_indicator_report','loan_officer_performance_report','audit_report','group_indicator_report') DEFAULT NULL,
  `start_date_type` enum('date_picker','today','yesterday','tomorrow') DEFAULT NULL,
  `start_date` date DEFAULT NULL,
  `end_date_type` enum('date_picker','today','yesterday','tomorrow') DEFAULT NULL,
  `end_date` date DEFAULT NULL,
  `office_id` varchar(191) DEFAULT NULL,
  `loan_officer_id` varchar(191) DEFAULT NULL,
  `gl_account_id` varchar(191) DEFAULT NULL,
  `manual_entries` varchar(191) DEFAULT NULL,
  `loan_status` varchar(191) DEFAULT NULL,
  `loan_product_id` varchar(191) DEFAULT NULL,
  `last_run_date` date DEFAULT NULL,
  `next_run_date` date DEFAULT NULL,
  `last_run_time` date DEFAULT NULL,
  `next_run_time` date DEFAULT NULL,
  `number_of_runs` int(11) NOT NULL DEFAULT 0,
  `active` tinyint(4) NOT NULL DEFAULT 1,
  `status` enum('pending','approved','declined') NOT NULL DEFAULT 'pending',
  `approved_by_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `report_scheduler_run_history`
--

CREATE TABLE `report_scheduler_run_history` (
  `id` int(10) UNSIGNED NOT NULL,
  `report_schedule_id` int(11) DEFAULT NULL,
  `report_start_date` date DEFAULT NULL,
  `report_start_time` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `roles`
--

CREATE TABLE `roles` (
  `id` int(10) UNSIGNED NOT NULL,
  `slug` varchar(191) NOT NULL,
  `name` varchar(191) NOT NULL,
  `time_limit` tinyint(4) NOT NULL DEFAULT 0,
  `from_time` varchar(191) DEFAULT NULL,
  `to_time` varchar(191) DEFAULT NULL,
  `access_days` text DEFAULT NULL,
  `permissions` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `role_users`
--

CREATE TABLE `role_users` (
  `user_id` int(10) UNSIGNED NOT NULL,
  `role_id` int(10) UNSIGNED NOT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `savings`
--

CREATE TABLE `savings` (
  `id` int(10) UNSIGNED NOT NULL,
  `client_type` enum('client','group') NOT NULL DEFAULT 'client',
  `client_id` int(11) NOT NULL,
  `old_client_id` varchar(191) DEFAULT NULL,
  `group_id` int(11) DEFAULT NULL,
  `office_id` int(11) DEFAULT NULL,
  `field_officer_id` int(11) DEFAULT NULL,
  `savings_product_id` int(11) DEFAULT NULL,
  `external_id` varchar(191) DEFAULT NULL,
  `account_number` varchar(191) DEFAULT NULL,
  `old_account_number` varchar(191) DEFAULT NULL,
  `currency_id` int(11) DEFAULT NULL,
  `decimals` int(11) NOT NULL DEFAULT 2,
  `interest_rate` decimal(65,4) DEFAULT NULL,
  `allow_overdraft` tinyint(4) NOT NULL DEFAULT 0,
  `minimum_balance` decimal(65,4) DEFAULT NULL,
  `overdraft_limit` decimal(65,4) DEFAULT NULL,
  `interest_compounding_period` enum('daily','monthly','quarterly','biannual','annually') DEFAULT NULL,
  `interest_posting_period` enum('monthly','quarterly','biannual','annually') DEFAULT NULL,
  `allow_transfer_withdrawal_fee` tinyint(4) NOT NULL DEFAULT 0,
  `opening_balance` decimal(65,4) DEFAULT NULL,
  `allow_additional_charges` tinyint(4) NOT NULL DEFAULT 0,
  `year_days` enum('360','365') NOT NULL DEFAULT '365',
  `status` enum('pending','approved','closed','declined','withdrawn') NOT NULL DEFAULT 'pending',
  `created_by_id` int(11) DEFAULT NULL,
  `modified_by_id` int(11) DEFAULT NULL,
  `approved_by_id` int(11) DEFAULT NULL,
  `closed_by_id` int(11) DEFAULT NULL,
  `declined_by_id` int(11) DEFAULT NULL,
  `created_date` date DEFAULT NULL,
  `modified_date` date DEFAULT NULL,
  `approved_date` date DEFAULT NULL,
  `declined_date` date DEFAULT NULL,
  `closed_date` date DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `approved_notes` text DEFAULT NULL,
  `declined_notes` text DEFAULT NULL,
  `closed_notes` text DEFAULT NULL,
  `balance` decimal(65,4) DEFAULT NULL,
  `deposits` decimal(65,4) DEFAULT NULL,
  `interest_earned` decimal(65,4) DEFAULT NULL,
  `interest_posted` decimal(65,4) DEFAULT NULL,
  `interest_overdraft` decimal(65,4) DEFAULT NULL,
  `withdrawals` decimal(65,4) DEFAULT NULL,
  `fees` decimal(65,4) DEFAULT NULL,
  `penalty` decimal(65,4) DEFAULT NULL,
  `start_interest_calculation_date` date DEFAULT NULL,
  `last_interest_calculation_date` date DEFAULT NULL,
  `next_interest_calculation_date` date DEFAULT NULL,
  `next_interest_posting_date` date DEFAULT NULL,
  `last_interest_posting_date` date DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ROW_FORMAT=COMPRESSED;

-- --------------------------------------------------------

--
-- Table structure for table `savings_charges`
--

CREATE TABLE `savings_charges` (
  `id` int(10) UNSIGNED NOT NULL,
  `savings_id` int(11) DEFAULT NULL,
  `charge_id` int(11) DEFAULT NULL,
  `penalty` tinyint(4) NOT NULL DEFAULT 0,
  `waived` tinyint(4) NOT NULL DEFAULT 0,
  `charge_type` enum('savings_activation','withdrawal_fee','annual_fee','monthly_fee','specified_due_date') NOT NULL,
  `charge_option` enum('flat','percentage') NOT NULL,
  `amount` decimal(65,2) DEFAULT NULL,
  `amount_paid` decimal(65,2) DEFAULT NULL,
  `due_date` date DEFAULT NULL,
  `grace_period` int(11) NOT NULL DEFAULT 0,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `savings_products`
--

CREATE TABLE `savings_products` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `name` varchar(191) DEFAULT NULL,
  `short_name` varchar(191) DEFAULT NULL,
  `description` text DEFAULT NULL,
  `currency_id` int(11) DEFAULT NULL,
  `decimals` int(11) NOT NULL DEFAULT 2,
  `interest_rate` decimal(65,4) DEFAULT NULL,
  `allow_overdraft` tinyint(4) NOT NULL DEFAULT 0,
  `minimum_balance` decimal(65,4) DEFAULT NULL,
  `interest_compounding_period` enum('daily','monthly','quarterly','biannual','annually') DEFAULT NULL,
  `interest_posting_period` enum('monthly','quarterly','biannual','annually') DEFAULT NULL,
  `interest_calculation_type` enum('daily','average') DEFAULT NULL,
  `allow_transfer_withdrawal_fee` tinyint(4) NOT NULL DEFAULT 0,
  `opening_balance` decimal(65,4) DEFAULT NULL,
  `allow_additional_charges` tinyint(4) NOT NULL DEFAULT 0,
  `year_days` enum('360','365') NOT NULL DEFAULT '365',
  `accounting_rule` enum('none','cash') NOT NULL DEFAULT 'cash',
  `gl_account_savings_reference_id` int(11) DEFAULT NULL,
  `gl_account_overdraft_portfolio_id` int(11) DEFAULT NULL,
  `gl_account_savings_control_id` int(11) DEFAULT NULL,
  `gl_account_interest_on_savings_id` int(11) DEFAULT NULL,
  `gl_account_savings_written_off_id` int(11) DEFAULT NULL,
  `gl_account_income_interest_id` int(11) DEFAULT NULL,
  `gl_account_income_fee_id` int(11) DEFAULT NULL,
  `gl_account_income_penalty_id` int(11) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `savings_product_charges`
--

CREATE TABLE `savings_product_charges` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `charge_id` int(11) DEFAULT NULL,
  `savings_product_id` int(11) DEFAULT NULL,
  `amount` decimal(65,2) DEFAULT NULL,
  `date` date DEFAULT NULL,
  `grace_period` int(11) NOT NULL DEFAULT 0,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `savings_transactions`
--

CREATE TABLE `savings_transactions` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `office_id` int(11) DEFAULT NULL,
  `modified_by_id` int(11) DEFAULT NULL,
  `payment_detail_id` int(11) DEFAULT NULL,
  `savings_id` int(10) UNSIGNED DEFAULT NULL,
  `amount` decimal(10,2) DEFAULT 0.00,
  `debit` decimal(65,4) DEFAULT NULL,
  `credit` decimal(65,4) DEFAULT NULL,
  `balance` decimal(65,4) DEFAULT NULL,
  `transaction_type` enum('deposit','withdrawal','bank_fees','interest','dividend','guarantee','guarantee_restored','fees_payment','transfer_loan','transfer_savings','specified_due_date_fee') DEFAULT NULL,
  `reversible` tinyint(4) NOT NULL DEFAULT 0,
  `reversed` tinyint(4) NOT NULL DEFAULT 0,
  `reversal_type` enum('system','user','none') NOT NULL DEFAULT 'none',
  `status` enum('pending','approved','declined') DEFAULT 'pending',
  `approved_by_id` int(11) DEFAULT NULL,
  `approved_date` date DEFAULT NULL,
  `system_interest` tinyint(4) NOT NULL DEFAULT 0,
  `date` date DEFAULT NULL,
  `time` varchar(191) DEFAULT NULL,
  `year` varchar(191) DEFAULT NULL,
  `month` varchar(191) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `balance_date` date DEFAULT NULL,
  `balance_days` int(11) DEFAULT NULL,
  `cumulative_balance_days` int(11) DEFAULT NULL,
  `cumulative_balance` decimal(65,4) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `settings`
--

CREATE TABLE `settings` (
  `id` int(10) UNSIGNED NOT NULL,
  `setting_key` varchar(191) NOT NULL,
  `setting_value` text DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `sms_gateways`
--

CREATE TABLE `sms_gateways` (
  `id` int(10) UNSIGNED NOT NULL,
  `created_by_id` int(11) DEFAULT NULL,
  `name` text DEFAULT NULL,
  `from_name` text DEFAULT NULL,
  `to_name` text DEFAULT NULL,
  `url` text DEFAULT NULL,
  `msg_name` text DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `throttle`
--

CREATE TABLE `throttle` (
  `id` int(10) UNSIGNED NOT NULL,
  `user_id` int(10) UNSIGNED DEFAULT NULL,
  `type` varchar(191) NOT NULL,
  `ip` varchar(191) DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `users`
--

CREATE TABLE `users` (
  `id` int(10) UNSIGNED NOT NULL,
  `office_id` bigint(20) DEFAULT NULL,
  `email` varchar(191) NOT NULL,
  `password` varchar(191) NOT NULL,
  `permissions` text DEFAULT NULL,
  `last_login` timestamp NULL DEFAULT NULL,
  `first_name` varchar(191) DEFAULT NULL,
  `last_name` varchar(191) DEFAULT NULL,
  `phone` varchar(191) DEFAULT NULL,
  `gender` enum('male','female','other','unspecified') DEFAULT 'unspecified',
  `enable_google2fa` tinyint(4) NOT NULL DEFAULT 0,
  `blocked` tinyint(4) NOT NULL DEFAULT 0,
  `google2fa_secret` text DEFAULT NULL,
  `address` text DEFAULT NULL,
  `notes` text DEFAULT NULL,
  `time_limit` tinyint(4) NOT NULL DEFAULT 0,
  `from_time` varchar(191) DEFAULT NULL,
  `to_time` varchar(191) DEFAULT NULL,
  `access_days` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Indexes for dumped tables
--

--
-- Indexes for table `activations`
--
ALTER TABLE `activations`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `assets`
--
ALTER TABLE `assets`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `asset_depreciation`
--
ALTER TABLE `asset_depreciation`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `asset_types`
--
ALTER TABLE `asset_types`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `audit_trail`
--
ALTER TABLE `audit_trail`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `charges`
--
ALTER TABLE `charges`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `clients`
--
ALTER TABLE `clients`
  ADD PRIMARY KEY (`id`),
  ADD KEY `client_id` (`client_id`);

--
-- Indexes for table `client_identifications`
--
ALTER TABLE `client_identifications`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `client_identification_types`
--
ALTER TABLE `client_identification_types`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `client_next_of_gaur`
--
ALTER TABLE `client_next_of_gaur`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `client_next_of_kin`
--
ALTER TABLE `client_next_of_kin`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `client_profession`
--
ALTER TABLE `client_profession`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `client_relationships`
--
ALTER TABLE `client_relationships`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `client_users`
--
ALTER TABLE `client_users`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `collateral`
--
ALTER TABLE `collateral`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `collateral_types`
--
ALTER TABLE `collateral_types`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `communication_campaigns`
--
ALTER TABLE `communication_campaigns`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `countries`
--
ALTER TABLE `countries`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `currencies`
--
ALTER TABLE `currencies`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `custom_fields`
--
ALTER TABLE `custom_fields`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `custom_fields_meta`
--
ALTER TABLE `custom_fields_meta`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `documents`
--
ALTER TABLE `documents`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `expenses`
--
ALTER TABLE `expenses`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `expense_budgets`
--
ALTER TABLE `expense_budgets`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `expense_types`
--
ALTER TABLE `expense_types`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `funds`
--
ALTER TABLE `funds`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `gl_accounts`
--
ALTER TABLE `gl_accounts`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `gl_closures`
--
ALTER TABLE `gl_closures`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `gl_journal_entries`
--
ALTER TABLE `gl_journal_entries`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `groups`
--
ALTER TABLE `groups`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `group_clients`
--
ALTER TABLE `group_clients`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `group_loan_allocation`
--
ALTER TABLE `group_loan_allocation`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `group_users`
--
ALTER TABLE `group_users`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `guarantors`
--
ALTER TABLE `guarantors`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loans`
--
ALTER TABLE `loans`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `account_number` (`account_number`),
  ADD KEY `client_id` (`client_id`);

--
-- Indexes for table `loan_applications`
--
ALTER TABLE `loan_applications`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loan_charges`
--
ALTER TABLE `loan_charges`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loan_products`
--
ALTER TABLE `loan_products`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loan_product_charges`
--
ALTER TABLE `loan_product_charges`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loan_provisioning_criteria`
--
ALTER TABLE `loan_provisioning_criteria`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loan_purposes`
--
ALTER TABLE `loan_purposes`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loan_repayment_schedules`
--
ALTER TABLE `loan_repayment_schedules`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loan_reschedule_requests`
--
ALTER TABLE `loan_reschedule_requests`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `loan_transactions`
--
ALTER TABLE `loan_transactions`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `payment_detail_id` (`payment_detail_id`),
  ADD UNIQUE KEY `loan_id` (`loan_id`,`client_id`),
  ADD KEY `status` (`status`);

--
-- Indexes for table `loan_transaction_repayment_schedule_mappings`
--
ALTER TABLE `loan_transaction_repayment_schedule_mappings`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `migrations`
--
ALTER TABLE `migrations`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `notes`
--
ALTER TABLE `notes`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `offices`
--
ALTER TABLE `offices`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `office_transactions`
--
ALTER TABLE `office_transactions`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `other_income`
--
ALTER TABLE `other_income`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `other_income_types`
--
ALTER TABLE `other_income_types`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `payment_details`
--
ALTER TABLE `payment_details`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `payment_types`
--
ALTER TABLE `payment_types`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `payment_type_details`
--
ALTER TABLE `payment_type_details`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `payroll`
--
ALTER TABLE `payroll`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `payroll_meta`
--
ALTER TABLE `payroll_meta`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `payroll_templates`
--
ALTER TABLE `payroll_templates`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `payroll_template_meta`
--
ALTER TABLE `payroll_template_meta`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `permissions`
--
ALTER TABLE `permissions`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `persistences`
--
ALTER TABLE `persistences`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `persistences_code_unique` (`code`);

--
-- Indexes for table `reminders`
--
ALTER TABLE `reminders`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `report_scheduler`
--
ALTER TABLE `report_scheduler`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `report_scheduler_run_history`
--
ALTER TABLE `report_scheduler_run_history`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `roles`
--
ALTER TABLE `roles`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `roles_slug_unique` (`slug`);

--
-- Indexes for table `role_users`
--
ALTER TABLE `role_users`
  ADD PRIMARY KEY (`user_id`,`role_id`);

--
-- Indexes for table `savings`
--
ALTER TABLE `savings`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `account_number` (`account_number`),
  ADD KEY `client_id` (`client_id`),
  ADD KEY `interest_rate` (`interest_rate`),
  ADD KEY `balance` (`balance`),
  ADD KEY `last_interest_posting_date` (`last_interest_posting_date`);

--
-- Indexes for table `savings_charges`
--
ALTER TABLE `savings_charges`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `savings_products`
--
ALTER TABLE `savings_products`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `savings_product_charges`
--
ALTER TABLE `savings_product_charges`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `savings_transactions`
--
ALTER TABLE `savings_transactions`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `payment_detail_id` (`payment_detail_id`),
  ADD KEY `balance` (`balance`),
  ADD KEY `transaction_type` (`transaction_type`),
  ADD KEY `savings_id` (`savings_id`,`amount`),
  ADD KEY `status` (`status`);

--
-- Indexes for table `settings`
--
ALTER TABLE `settings`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `sms_gateways`
--
ALTER TABLE `sms_gateways`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `throttle`
--
ALTER TABLE `throttle`
  ADD PRIMARY KEY (`id`),
  ADD KEY `throttle_user_id_index` (`user_id`);

--
-- Indexes for table `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `users_email_unique` (`email`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `activations`
--
ALTER TABLE `activations`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `assets`
--
ALTER TABLE `assets`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `asset_depreciation`
--
ALTER TABLE `asset_depreciation`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `asset_types`
--
ALTER TABLE `asset_types`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `audit_trail`
--
ALTER TABLE `audit_trail`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `charges`
--
ALTER TABLE `charges`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `clients`
--
ALTER TABLE `clients`
  MODIFY `id` int(250) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `client_identifications`
--
ALTER TABLE `client_identifications`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `client_identification_types`
--
ALTER TABLE `client_identification_types`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `client_next_of_gaur`
--
ALTER TABLE `client_next_of_gaur`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `client_next_of_kin`
--
ALTER TABLE `client_next_of_kin`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `client_profession`
--
ALTER TABLE `client_profession`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `client_relationships`
--
ALTER TABLE `client_relationships`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `client_users`
--
ALTER TABLE `client_users`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `collateral`
--
ALTER TABLE `collateral`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `collateral_types`
--
ALTER TABLE `collateral_types`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `communication_campaigns`
--
ALTER TABLE `communication_campaigns`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `countries`
--
ALTER TABLE `countries`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `currencies`
--
ALTER TABLE `currencies`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `custom_fields`
--
ALTER TABLE `custom_fields`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `custom_fields_meta`
--
ALTER TABLE `custom_fields_meta`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `documents`
--
ALTER TABLE `documents`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `expenses`
--
ALTER TABLE `expenses`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `expense_budgets`
--
ALTER TABLE `expense_budgets`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `expense_types`
--
ALTER TABLE `expense_types`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `funds`
--
ALTER TABLE `funds`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `gl_accounts`
--
ALTER TABLE `gl_accounts`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `gl_closures`
--
ALTER TABLE `gl_closures`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `gl_journal_entries`
--
ALTER TABLE `gl_journal_entries`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `groups`
--
ALTER TABLE `groups`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `group_clients`
--
ALTER TABLE `group_clients`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `group_loan_allocation`
--
ALTER TABLE `group_loan_allocation`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `group_users`
--
ALTER TABLE `group_users`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `guarantors`
--
ALTER TABLE `guarantors`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loans`
--
ALTER TABLE `loans`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_applications`
--
ALTER TABLE `loan_applications`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_charges`
--
ALTER TABLE `loan_charges`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_products`
--
ALTER TABLE `loan_products`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_product_charges`
--
ALTER TABLE `loan_product_charges`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_provisioning_criteria`
--
ALTER TABLE `loan_provisioning_criteria`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_purposes`
--
ALTER TABLE `loan_purposes`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_repayment_schedules`
--
ALTER TABLE `loan_repayment_schedules`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_reschedule_requests`
--
ALTER TABLE `loan_reschedule_requests`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_transactions`
--
ALTER TABLE `loan_transactions`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `loan_transaction_repayment_schedule_mappings`
--
ALTER TABLE `loan_transaction_repayment_schedule_mappings`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `migrations`
--
ALTER TABLE `migrations`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `notes`
--
ALTER TABLE `notes`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `offices`
--
ALTER TABLE `offices`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `office_transactions`
--
ALTER TABLE `office_transactions`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `other_income`
--
ALTER TABLE `other_income`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `other_income_types`
--
ALTER TABLE `other_income_types`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `payment_details`
--
ALTER TABLE `payment_details`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `payment_types`
--
ALTER TABLE `payment_types`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `payment_type_details`
--
ALTER TABLE `payment_type_details`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `payroll`
--
ALTER TABLE `payroll`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `payroll_meta`
--
ALTER TABLE `payroll_meta`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `payroll_templates`
--
ALTER TABLE `payroll_templates`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `payroll_template_meta`
--
ALTER TABLE `payroll_template_meta`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `permissions`
--
ALTER TABLE `permissions`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `persistences`
--
ALTER TABLE `persistences`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `reminders`
--
ALTER TABLE `reminders`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `report_scheduler`
--
ALTER TABLE `report_scheduler`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `report_scheduler_run_history`
--
ALTER TABLE `report_scheduler_run_history`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `roles`
--
ALTER TABLE `roles`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `savings`
--
ALTER TABLE `savings`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `savings_charges`
--
ALTER TABLE `savings_charges`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `savings_products`
--
ALTER TABLE `savings_products`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `savings_product_charges`
--
ALTER TABLE `savings_product_charges`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `savings_transactions`
--
ALTER TABLE `savings_transactions`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `settings`
--
ALTER TABLE `settings`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `sms_gateways`
--
ALTER TABLE `sms_gateways`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `throttle`
--
ALTER TABLE `throttle`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `users`
--
ALTER TABLE `users`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
